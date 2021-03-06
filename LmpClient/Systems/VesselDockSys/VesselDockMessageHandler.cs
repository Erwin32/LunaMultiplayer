﻿using System;
using System.Collections.Concurrent;
using LmpClient.Base;
using LmpClient.Base.Interface;
using LmpClient.Extensions;
using LmpClient.Systems.VesselRemoveSys;
using LmpClient.Systems.Warp;
using LmpClient.VesselUtilities;
using LmpCommon.Message.Data.Vessel;
using LmpCommon.Message.Interface;
using LmpCommon.Time;

namespace LmpClient.Systems.VesselDockSys
{
    public class VesselDockMessageHandler : SubSystem<VesselDockSystem>, IMessageHandler
    {
        private static WarpSystem WarpSystem => WarpSystem.Singleton;
        public ConcurrentQueue<IServerMessageBase> IncomingMessages { get; set; } = new ConcurrentQueue<IServerMessageBase>();

        public void HandleMessage(IServerMessageBase msg)
        {
            if (!(msg.Data is VesselDockMsgData msgData)) return;

            LunaLog.Log("Docking message received!");

            if (msgData.WeakVesselId == CurrentDockEvent.WeakVesselId && msgData.DominantVesselId == CurrentDockEvent.DominantVesselId && 
                (LunaNetworkTime.UtcNow - CurrentDockEvent.DockingTime) < TimeSpan.FromSeconds(5))
            {
                LunaLog.Log("Docking message received was detected so ignore it");
                return;
            }

            var dominantProto = VesselSerializer.DeserializeVessel(msgData.FinalVesselData, msgData.NumBytes);
            VesselLoader.LoadVessel(dominantProto);

            WarpSystem.WarpIfSubspaceIsMoreAdvanced(msgData.SubspaceId);

            if (FlightGlobals.ActiveVessel && FlightGlobals.ActiveVessel.id == msgData.WeakVesselId)
            {
                LunaLog.Log($"Docking NOT detected. We DON'T OWN the dominant vessel. Switching to {msgData.DominantVesselId}");
                if (dominantProto.vesselRef != null)
                {
                    dominantProto.vesselRef.Load();
                    dominantProto.vesselRef.GoOffRails();
                    FlightGlobals.ForceSetActiveVessel(dominantProto.vesselRef);
                }
            }
            else if (FlightGlobals.ActiveVessel && FlightGlobals.ActiveVessel.id == msgData.DominantVesselId)
            {
                LunaLog.Log("Docking NOT detected. We OWN the dominant vessel");
            }

            VesselRemoveSystem.Singleton.KillVessel(msgData.WeakVesselId, "Killing weak (active) vessel during a docking that was not detected");
            CurrentDockEvent.Reset();
        }
    }
}
