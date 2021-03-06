﻿using LmpClient.Base;
using LmpClient.Localization;
using LmpClient.Systems.Lock;
using LmpClient.Systems.SettingsSys;
using LmpClient.VesselUtilities;
using System;

namespace LmpClient.Systems.KerbalSys
{
    public class KerbalEvents : SubSystem<KerbalSystem>
    {
        private static Guid _recoveringTerminatingVesselId = Guid.Empty;

        /// <summary>
        /// Use this event to send the kerbals just when we start a flight.
        /// We use this event instead of onFlightReady as the latter is triggered once UI and everythign is ready and this one is triggered
        /// earlier in the chain
        /// </summary>
        public void SwitchSceneRequested(GameScenes data)
        {
            if (data == GameScenes.FLIGHT)
            {
                var crew = FlightDriver.newShipManifest;
                if (crew == null) return;

                foreach (var protoCrew in crew.GetAllCrew(false))
                {
                    if (protoCrew == null) continue;

                    //Always set the kerbals in a vessel as assigned
                    System.SetKerbalStatusWithoutTriggeringEvent(protoCrew, ProtoCrewMember.RosterStatus.Assigned);
                    System.MessageSender.SendKerbal(protoCrew);
                    LockSystem.Singleton.AcquireKerbalLock(protoCrew.name, true);
                }
            }
        }

        /// <summary>
        /// Event triggered when any kerbal status changes (kerbal dies, etc)
        /// </summary>
        public void StatusChange(ProtoCrewMember kerbal, ProtoCrewMember.RosterStatus previousStatus, ProtoCrewMember.RosterStatus newStatus)
        {
            if (previousStatus != newStatus)
            {
                //This is the case when we are removing a vessel from another player. This status change event will be called
                if (LockSystem.LockQuery.KerbalLockExists(kerbal.name) && !LockSystem.LockQuery.KerbalLockBelongsToPlayer(kerbal.name, SettingsSystem.CurrentSettings.PlayerName))
                {
                    System.SetKerbalStatusWithoutTriggeringEvent(kerbal, previousStatus);
                    return;
                }

                System.SetKerbalStatusWithoutTriggeringEvent(kerbal, newStatus);
                System.MessageSender.SendKerbal(kerbal);
                System.RefreshCrewDialog();
            }
        }

        /// <summary>
        /// This event is triggered when we hire a kerbal (previous type was applicant, new is crew)
        /// Also triggered when we sack a kerbal (previous type was crew, new is applicant)
        /// </summary>
        public void TypeChange(ProtoCrewMember kerbal, ProtoCrewMember.KerbalType previousType, ProtoCrewMember.KerbalType newType)
        {
            if (previousType != newType)
            {
                if (LockSystem.LockQuery.KerbalLockExists(kerbal.name) && !LockSystem.LockQuery.KerbalLockBelongsToPlayer(kerbal.name, SettingsSystem.CurrentSettings.PlayerName))
                {
                    LunaScreenMsg.PostScreenMessage(LocalizationContainer.ScreenText.KerbalNotYours, 5f, ScreenMessageStyle.UPPER_CENTER);
                    System.SetKerbalTypeWithoutTriggeringEvent(kerbal, ProtoCrewMember.KerbalType.Crew);
                    return;
                }

                System.SetKerbalTypeWithoutTriggeringEvent(kerbal, newType);
                System.MessageSender.SendKerbal(kerbal);
                System.RefreshCrewDialog();
            }
        }

        /// <summary>
        /// When returning to editor force all kerbals as available.
        /// Bear in mind that we will NOT have the kerbal lock!
        /// </summary>
        public void ReturningToEditor(EditorFacility data)
        {
            if (FlightGlobals.ActiveVessel == null || VesselCommon.IsSpectating) return;

            //Force setting the kerbals as available as when reverting their status will be "assigned"
            var kerbals = FlightGlobals.ActiveVessel.GetVesselCrew();
            foreach (var kerbal in kerbals)
            {
                System.SetKerbalStatusWithoutTriggeringEvent(kerbal, ProtoCrewMember.RosterStatus.Available);
                System.MessageSender.SendKerbal(kerbal);
            }
        }

        /// <summary>
        /// Force setting the kerbals as missing in a terminated vessel
        /// </summary>
        public void OnVesselTerminated(ProtoVessel terminatedVessel)
        {
            if (terminatedVessel == null) return;

            if (LockSystem.LockQuery.CanRecoverOrTerminateTheVessel(terminatedVessel.vesselID, SettingsSystem.CurrentSettings.PlayerName))
            {
                _recoveringTerminatingVesselId = terminatedVessel.vesselID;

                //Force setting the kerbals as missing as we don't have their kerbal lock
                var kerbals = terminatedVessel.GetVesselCrew();
                foreach (var kerbal in kerbals)
                {
                    KerbalSystem.Singleton.SetKerbalStatusWithoutTriggeringEvent(kerbal, ProtoCrewMember.RosterStatus.Missing);
                    System.MessageSender.SendKerbal(kerbal);
                }
            }
        }

        /// <summary>
        /// Force setting the kerbals as available in a recovered vessel
        /// </summary>
        public void OnVesselRecovered(ProtoVessel recoveredVessel, bool quick)
        {
            if (recoveredVessel == null) return;

            if (LockSystem.LockQuery.CanRecoverOrTerminateTheVessel(recoveredVessel.vesselID, SettingsSystem.CurrentSettings.PlayerName))
            {
                _recoveringTerminatingVesselId = recoveredVessel.vesselID;

                //Force setting the kerbals as missing as we don't have their kerbal lock
                var kerbals = recoveredVessel.GetVesselCrew();
                foreach (var kerbal in kerbals)
                {
                    KerbalSystem.Singleton.SetKerbalStatusWithoutTriggeringEvent(kerbal, ProtoCrewMember.RosterStatus.Available);
                    System.MessageSender.SendKerbal(kerbal);
                }
            }
        }

        public void OnVesselWillDestroy(Vessel dyingVessel)
        {
            if (dyingVessel == null) return;

            //Only send the vessel remove msg if we own the unloaded update lock
            if (LockSystem.LockQuery.UnloadedUpdateLockBelongsToPlayer(dyingVessel.id, SettingsSystem.CurrentSettings.PlayerName) || dyingVessel.id == _recoveringTerminatingVesselId)
            {
                foreach (var protoCrew in dyingVessel.GetVesselCrew())
                {
                    System.MessageSender.SendKerbal(protoCrew);
                }
            }
        }

        /// <summary>
        /// Whenever we load a vessel trigger a refresh of the astronaut complex if we are in it
        /// </summary>
        public void OnVesselLoaded(Vessel data)
        {
            if (System.AstronautComplex != null)
            {
                System.RefreshCrewDialog();
            }

            //Trigger a ValidateAssignments so missing crews are set as assigned
            HighLogic.CurrentGame.Updated();
        }
    }
}
