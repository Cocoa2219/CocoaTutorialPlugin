using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Components;
using Exiled.API.Features.Pools;
using Exiled.API.Features.Roles;
using static HarmonyLib.AccessTools;
using HarmonyLib;
using MEC;
using Mirror;
using PlayerRoles;
using RelativePositioning;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CocoaTutorialPlugin.Features;

public static class Utils
{
    private static Player Scp079Ping { get; set; }

    public static void Ping(Vector3 position, PingType type = PingType.Default)
    {
        var wait = Scp079Ping == null ? 0.5f : 0f;

        Scp079Ping ??= Player.Get(SpawnNpc("SCP-079", "ping@localhost"));

        Timing.CallDelayed(wait, () =>
        {
            Scp079Ping.Role.Set(RoleTypeId.Scp079);

            var scp079 = Scp079Ping.Role.As<Scp079Role>();

            scp079.PingAbility._syncPos = new RelativePosition(position);
            scp079.PingAbility._syncNormal = position;
            scp079.PingAbility._syncProcessorIndex = (byte) type;

            scp079.PingAbility.ServerSendRpc(true);
        });
    }

    public static void Ping(Vector3 position, PingType type = PingType.Default, params Player[] players)
    {
        var wait = Scp079Ping == null ? 0.5f : 0f;

        Scp079Ping ??= Player.Get(SpawnNpc("SCP-079", "ping@localhost"));

        Timing.CallDelayed(wait, () =>
        {
            Scp079Ping.Role.Set(RoleTypeId.Scp079);

            var scp079 = Scp079Ping.Role.As<Scp079Role>();

            scp079.PingAbility._syncPos = new RelativePosition(position);
            scp079.PingAbility._syncNormal = position;
            scp079.PingAbility._syncProcessorIndex = (byte) type;

            scp079.PingAbility.ServerSendRpc(x => players.Select(y => y.ReferenceHub).Contains(x));
        });
    }

    public static GameObject SpawnNpc(string name, string userId, int id = 0)
    {
        var newObject = Object.Instantiate(NetworkManager.singleton.playerPrefab);

        var referenceHub = newObject.GetComponent<ReferenceHub>();

        try
        {
            referenceHub.roleManager.InitializeNewRole(RoleTypeId.None, RoleChangeReason.None);
        }
        catch (Exception e)
        {
            Log.Debug($"Ignore: {e}");
        }

        if (RecyclablePlayerId.FreeIds.Contains(id))
        {
            RecyclablePlayerId.FreeIds.RemoveFromQueue(id);
        }
        else if (RecyclablePlayerId._autoIncrement >= id)
        {
            RecyclablePlayerId._autoIncrement = id = RecyclablePlayerId._autoIncrement + 1;
        }

        FakeConnection fakeConnection = new(id);
        NetworkServer.AddPlayerForConnection(fakeConnection, newObject);
        try
        {
            referenceHub.authManager.UserId = string.IsNullOrEmpty(userId) ? $"Dummy@localhost" : userId;
        }
        catch (Exception e)
        {
            Log.Debug($"Ignore: {e}");
        }

        referenceHub.nicknameSync.Network_myNickSync = name;
        return newObject;
    }
}

[HarmonyPatch(typeof(ServerConsole), nameof(ServerConsole.RefreshOnlinePlayers))]
internal static class NpcServerCountFix
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var newInstructions = ListPool<CodeInstruction>.Pool.Get(instructions);

        const int offset = 0;
        var index = offset + newInstructions.FindIndex(i => i.opcode == OpCodes.Ldsflda);

        var labelcontinue = (Label)newInstructions[index - 1].operand;

        newInstructions.InsertRange(index, new[]
        {
            new CodeInstruction(OpCodes.Ldloc_1),
            new(OpCodes.Call, Method(typeof(Player), nameof(Player.Get), new[] { typeof(ReferenceHub) })),
            new(OpCodes.Callvirt, PropertyGetter(typeof(Player), nameof(Player.UserId))),
            new(OpCodes.Ldstr, "@localhost"),
            new(OpCodes.Call, Method(typeof(NpcServerCountFix), nameof(EndsWith))),
            new(OpCodes.Brtrue_S, labelcontinue),
        });

        for (var z = 0; z < newInstructions.Count; z++)
            yield return newInstructions[z];

        ListPool<CodeInstruction>.Pool.Return(newInstructions);
    }

    public static bool EndsWith(string target, string value) => target.EndsWith(value);
}