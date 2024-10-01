using System;
using System.Collections.Generic;
using System.Linq;
using CocoaTutorialPlugin.Features;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HarmonyLib;
using MEC;
using PlayerRoles;

namespace CocoaTutorialPlugin
{
    public class Plugin : Plugin<Config>
    {
        public override string Name { get; } = "CocoaTutorialPlugin";
        public override string Author { get; } = "Cocoa";
        public override string Prefix { get; } = "CocoaTutorialPlugin";
        public override Version Version { get; } = new(1, 0, 0);

        private Harmony Harmony { get; set; }

        public override void OnEnabled()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
            Exiled.Events.Handlers.Player.Verified += OnVerified;
            Exiled.Events.Handlers.Player.Left += OnLeft;
            base.OnEnabled();

            Harmony = new Harmony("cocoa.tutorial." + DateTime.Now.Ticks);
            Harmony.PatchAll();
        }

        public override void OnDisabled()
        {
            Harmony.UnpatchAll();
            Harmony = null;

            Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
            Exiled.Events.Handlers.Player.Verified -= OnVerified;
            Exiled.Events.Handlers.Player.Left -= OnLeft;
            base.OnDisabled();
        }

        private void OnWaitingForPlayers()
        {
            Round.Start();
            Round.IsLocked = true;
        }

        private void OnVerified(VerifiedEventArgs ev)
        {
            if (ev.Player.UserId.EndsWith("@localhost")) return;

            ev.Player.Role.Set(RoleTypeId.ClassD);
            var manager = ev.Player.GameObject.AddComponent<TutorialManager>();

            if (manager != null)
                manager.StartTutorial();
        }

        private void OnLeft(LeftEventArgs ev)
        {
            var manager = ev.Player.GameObject.GetComponent<TutorialManager>();
            if (manager != null)
                manager.Destroy();
        }
    }
}