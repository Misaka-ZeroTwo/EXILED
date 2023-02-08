﻿// -----------------------------------------------------------------------
// <copyright file="FixLateJoin.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------
namespace Exiled.Events.Patches.Generic
{
    using System.Collections.Generic;
    using System.Reflection.Emit;

    using Exiled.API.Features.Pools;
    using GameCore;
    using HarmonyLib;
    using InventorySystem.Items.Firearms.Modules;
    using MEC;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.RoleAssign;
    using UnityEngine;

    using static HarmonyLib.AccessTools;

    using AccessTools = HarmonyLib.AccessTools;
    using Log = Exiled.API.Features.Log;

    /// <summary>
    ///     Patches <see cref="SingleBulletHitreg.ServerProcessRaycastHit(Ray, RaycastHit)" />.
    ///     Adds the <see cref="Handlers.Player.Shooting" /> and <see cref="Handlers.Player.Shot" /> events.
    /// </summary>
    [HarmonyPatch]
    internal static class FixLateJoin
    {
        [HarmonyPatch(typeof(RoleAssigner), nameof(RoleAssigner.CheckLateJoin))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ShotBullet(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var newInstructions = ListPool<CodeInstruction>.Pool.Get(instructions);
            var returnLabel = generator.DefineLabel();
            newInstructions[newInstructions.Count - 1].WithLabels(returnLabel);
            newInstructions.InsertRange(
                0,
                new CodeInstruction[]
                {
                    // Player.Get(this.Hub)
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldarg_1),

                    new(OpCodes.Call,
                        Method(typeof(FixLateJoin), nameof(ProcessJoin))),

                    // if (!ev.CanHurt)
                    //    return;
                    new(OpCodes.Br, returnLabel),
                });

            for (var z = 0; z < newInstructions.Count; z++)
                yield return newInstructions[z];

            ListPool<CodeInstruction>.Pool.Return(newInstructions);
        }

        private static void ProcessJoin(ReferenceHub hub, ClientInstanceMode clientInstance)
        {
            Log.Debug("ProcessJoin entry");
            if (!NetworkServer.active || !RoleAssigner.CheckPlayer(hub) || !RoleAssigner._spawned)
            {
                Log.Debug($"ProcessJoin: Server not active/RoleAssigner.CheckPlayer(hub):{RoleAssigner.CheckPlayer(hub)}/RoleAssigner._spawned:{RoleAssigner._spawned}");
                return;
            }

            if (RoleAssigner.LateJoinTimer.Elapsed.TotalSeconds >
                ConfigFile.ServerConfig.GetFloat("late_join_time"))
            {
                Log.Debug($"ProcessJoin: Elapsed.TotalSeconds > late_join_time");
                hub.roleManager.ServerSetRole(RoleTypeId.Spectator, RoleChangeReason.RoundStart);
            }
            else
            {
                hub.roleManager.ServerSetRole(RoleTypeId.Spectator, RoleChangeReason.RoundStart);
                Log.Debug($"ProcessJoin: Elapsed.TotalSeconds < late_join_time, set player to spectator first.");
                Timing.CallDelayed(.5f, () =>
                {
                    Log.Debug($"ProcessJoin: Elapsed.TotalSeconds < late_join_time, delayed SpawnLate call");
                    HumanSpawner.SpawnLate(hub);
                });
                Log.Debug($"ProcessJoin: Elapsed.TotalSeconds < late_join_time, past timing.call delayed");
            }
        }
    }
}