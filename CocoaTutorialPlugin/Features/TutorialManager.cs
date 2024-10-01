using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using MEC;
using PlayerRoles;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CocoaTutorialPlugin.Features;

public class TutorialManager : MonoBehaviour
{
    public Player Player { get; private set; }

    private List<TutorialStep> Steps { get; } = new();

    internal Player Npc { get; set; }

    private List<CoroutineHandle> Coroutines { get; } = new();

    public void StartTutorial()
    {
        Player = Player.Get(gameObject);

        Steps.Clear();

        Npc = Player.Get(Utils.SpawnNpc("훈련병", "tutorial@localhost"));

        Timing.CallDelayed(0.5f, () =>
        {
            Npc.Role.Set(RoleTypeId.FacilityGuard, SpawnReason.RoundStart, RoleSpawnFlags.All);
            Npc.Position = Player.Position + Vector3.up * 0.1f;

            // Add tutorial steps here
            Steps.Add(new SequentialTutorialStep(this, "Step 1", "This is step 1", [
                new SequentialTutorialSubstep(this, "PickupItem", [("Pickup an item", 5f)],
                    () =>
                    {
                        var pickup = Pickup.CreateAndSpawn(ItemType.GunCOM15, Player.Position + Vector3.up * 0.2f, Quaternion.identity, Player);
                        return pickup.Serial;
                    },
                    serial =>
                    {
                        return Player.Items.Any(x => x.Serial == (ushort)serial);
                    }),
                new SequentialTutorialSubstep(this, "KillNpc", [
                    ("Kill the NPC", 5f),
                    ("Kill the NPC after 5 seconds", 5f)
                ], null, _ => Npc.ReferenceHub.GetRoleId() == RoleTypeId.Spectator)
            ]));

            Timing.CallDelayed(0.5f, () => RunCoroutine(ExecuteTutorial()));
        });
    }

    internal CoroutineHandle RunCoroutine(IEnumerator<float> coroutine)
    {
        var handle = Timing.RunCoroutine(coroutine);
        Coroutines.Add(handle);
        return handle;
    }

    public void Destroy()
    {
        foreach (var coroutine in Coroutines) Timing.KillCoroutines(coroutine);

        Coroutines.Clear();

        var conn = Npc.ReferenceHub.connectionToClient;
        if (Npc.ReferenceHub._playerId.Value <= RecyclablePlayerId._autoIncrement)
            Npc.ReferenceHub._playerId.Destroy();
        Npc.ReferenceHub.OnDestroy();
        CustomNetworkManager.TypedSingleton.OnServerDisconnect(conn);
        Destroy(Npc.GameObject);

        Destroy(this);
    }

    private IEnumerator<float> ExecuteTutorial()
    {
        foreach (var step in Steps) yield return Timing.WaitUntilDone(step.Execute());
    }
}

public abstract class TutorialStep(TutorialManager manager, string title)
{
    public string Title { get; } = title;
    internal TutorialManager Manager { get; set; } = manager;
    internal Player Player => Manager.Player;

    public abstract IEnumerator<float> Execute();
}

public class SequentialTutorialSubstep(
    TutorialManager manager,
    string title,
    List<(string text, float time)> texts,
    Func<object> action,
    Func<object, bool> condition) : TutorialStep(manager, title)
{
    private Func<object> Action { get; } = action;
    private List<(string text, float time)> Texts { get; } = texts;
    private Func<object, bool> Condition { get; } = condition;

    public override IEnumerator<float> Execute()
    {
        Log.Info($"Executing substep {Title}");
        var result = Action?.Invoke();
        var textCoroutine = Manager.RunCoroutine(ShowTexts());
        yield return Timing.WaitUntilTrue(() => Condition(result));
        Timing.KillCoroutines(textCoroutine);
        Player.ShowHint("");
        Log.Info($"Substep {Title} completed.");
    }

    private IEnumerator<float> ShowTexts()
    {
        foreach (var (text, time) in Texts)
        {
            Log.Debug($"Showing text: {text}");
            Player.ShowHint(text, time + 0.1f);
            yield return Timing.WaitForSeconds(time);
        }

        Player.ShowHint("");
    }
}

public class SequentialTutorialStep(
    TutorialManager manager,
    string title,
    string text,
    List<SequentialTutorialSubstep> steps) : TutorialStep(manager, title)
{
    private List<SequentialTutorialSubstep> Steps { get; } = steps;

    public override IEnumerator<float> Execute()
    {
        foreach (var step in Steps) yield return Timing.WaitUntilDone(step.Execute());
    }
}

public class ParallelTutorialStep(
    TutorialManager manager,
    string title,
    List<(string text, float time)> texts,
    List<TutorialStep> steps,
    Action action) : TutorialStep(manager, title)
{
    private Action Action { get; } = action;
    private List<TutorialStep> Steps { get; } = steps;
    private List<(string text, float time)> Texts { get; } = texts;

    private IEnumerator<float> ShowTexts()
    {
        foreach (var (text, time) in Texts)
        {
            Player.ShowHint(text, time + 0.1f);
            yield return Timing.WaitForSeconds(time);
        }

        Player.ShowHint("");
    }

    public override IEnumerator<float> Execute()
    {
        Action?.Invoke();

        var textCoroutine = Manager.RunCoroutine(ShowTexts());

        var coroutines = new List<CoroutineHandle>();

        foreach (var step in Steps)
        {
            var handle = Manager.RunCoroutine(step.Execute());

            if (handle is { IsRunning: true, IsValid: true })
                coroutines.Add(handle);
        }

        yield return Timing.WaitUntilTrue(() => coroutines.All(x => !x.IsRunning));

        Timing.KillCoroutines(textCoroutine);

        Player.ShowHint("");
    }
}

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class ShowHint : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, [UnscopedRef] out string response)
    {
        if (arguments.Count < 2)
        {
            response = "사용법: showhint <player> <text>";
            return false;
        }

        var player = Player.Get(arguments.At(0));

        if (player == null)
        {
            response = "플레이어를 찾을 수 없습니다.";
            return false;
        }

        player.ShowHint(string.Join(" ", arguments.Skip(1)), 20f);
        response = "성공적으로 힌트를 표시했습니다.";
        return true;
    }

    public string Command { get; } = "showhint";
    public string[] Aliases { get; } = { "sh" };
    public string Description { get; } = "플레이어에게 힌트를 표시합니다.";
}