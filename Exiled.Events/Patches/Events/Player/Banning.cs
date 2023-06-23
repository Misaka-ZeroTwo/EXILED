// -----------------------------------------------------------------------
// <copyright file="Banning.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace Exiled.Events.Patches.Events.Player
{
#pragma warning disable SA1313
    using System;

    using API.Features;

    using CommandSystem;

    using Exiled.Events.EventArgs.Player;
    using Footprinting;

    using HarmonyLib;

    using PluginAPI.Enums;
    using PluginAPI.Events;
    using RemoteAdmin;

    using Log = API.Features.Log;

    /// <summary>
    ///     Patches <see cref="BanPlayer.BanUser(ReferenceHub, ICommandSender, string, long)" />.
    ///     Adds the <see cref="Handlers.Player.Banning" /> event.
    /// </summary>
    [HarmonyPatch(typeof(BanPlayer), nameof(BanPlayer.BanUser), typeof(ReferenceHub), typeof(ICommandSender), typeof(string), typeof(long))]
    internal static class Banning
    {
        private static bool Prefix(Footprint target, ICommandSender issuer, string reason, long duration, ref bool __result)
        {
            try
            {
                ReferenceHub referenceHub;

                if (duration == 0L)
                {
                    __result = BanPlayer.KickUser(target.Hub, issuer, reason);
                    return false;
                }

                if (duration > long.MaxValue)
                    duration = long.MaxValue;

                if (target.Hub.serverRoles.BypassStaff)
                {
                    __result = false;
                    return false;
                }

                PlayerCommandSender playerCommandSender = issuer as PlayerCommandSender;
                referenceHub = playerCommandSender != null ? playerCommandSender.ReferenceHub : ReferenceHub.HostHub;
                PlayerBannedEvent playerBannedEvent = new PlayerBannedEvent(target.Hub, referenceHub, reason, duration);

                long issuanceTime = TimeBehaviour.CurrentTimestamp();
                long banExpirationTime = TimeBehaviour.GetBanExpirationTime((uint)duration);
                string originalName = BanPlayer.ValidateNick(target.Hub.nicknameSync.MyNick);
                string message = $"You have been banned. {(!string.IsNullOrEmpty(reason) ? "Reason: " + reason : string.Empty)}";

                BanningEventArgs ev = new(Player.Get(target), Player.Get(issuer), duration, reason, message);

                Handlers.Player.OnBanning(ev);

                if (!ev.IsAllowed)
                {
                    __result = false;
                    return false;
                }

                duration = ev.Duration;
                reason = ev.Reason;
                message = ev.FullMessage;

                if (!EventManager.ExecuteEvent(new PlayerBannedEvent(target.Hub, ev.Player.ReferenceHub, reason, duration)))
                {
                    __result = false;
                    return false;
                }

                duration = playerBannedEvent.Duration;
                reason = playerBannedEvent.Reason;

                BanPlayer.ApplyIpBan(target, issuer, reason, duration);

                BanHandler.IssueBan(
                    new BanDetails
                    {
                        OriginalName = originalName,
                        Id = target.Hub.characterClassManager.UserId,
                        IssuanceTime = issuanceTime,
                        Expires = banExpirationTime,
                        Reason = reason,
                        Issuer = issuer.LogName,
                    }, BanHandler.BanType.UserId);

                if (!string.IsNullOrEmpty(target.Hub.characterClassManager.UserId2))
                {
                    BanHandler.IssueBan(
                        new BanDetails
                        {
                            OriginalName = originalName,
                            Id = target.Hub.characterClassManager.UserId2,
                            IssuanceTime = issuanceTime,
                            Expires = banExpirationTime,
                            Reason = reason,
                            Issuer = issuer.LogName,
                        }, BanHandler.BanType.UserId);
                }

                ServerConsole.Disconnect(target.Hub.gameObject, message);

                __result = true;
                return false;
            }
            catch (Exception exception)
            {
                Log.Error($"Exiled.Events.Patches.Events.Player.Banning: {exception}\n{exception.StackTrace}");
                return true;
            }
        }
    }
}