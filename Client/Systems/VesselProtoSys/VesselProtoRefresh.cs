﻿using System.Collections.Generic;
using System.Reflection;
using UniLinq;

namespace LunaClient.Systems.VesselProtoSys
{
    public class VesselProtoRefresh
    {
        private static readonly FieldInfo CrewField = typeof(ProtoVessel).GetField("crew", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void RefreshVesselProto(Vessel vessel)
        {
            if (vessel.protoVessel.vesselID != vessel.id)
            {
                LunaLog.LogError($"Cannot update a protovessel id {vessel.protoVessel.vesselID} from a vessel with id {vessel.id}");
                return;
            }

            vessel.protoVessel.vesselRef = vessel;

            vessel.protoVessel.orbitSnapShot.semiMajorAxis = vessel.orbit.semiMajorAxis;
            vessel.protoVessel.orbitSnapShot.eccentricity = vessel.orbit.eccentricity;
            vessel.protoVessel.orbitSnapShot.inclination = vessel.orbit.inclination;
            vessel.protoVessel.orbitSnapShot.argOfPeriapsis = vessel.orbit.argumentOfPeriapsis;
            vessel.protoVessel.orbitSnapShot.LAN = vessel.orbit.LAN;
            vessel.protoVessel.orbitSnapShot.meanAnomalyAtEpoch = vessel.orbit.meanAnomalyAtEpoch;
            vessel.protoVessel.orbitSnapShot.epoch = vessel.orbit.epoch;
            vessel.protoVessel.orbitSnapShot.ReferenceBodyIndex = FlightGlobals.Bodies.IndexOf(vessel.orbit.referenceBody);

            vessel.protoVessel.vesselID = vessel.id;
            vessel.protoVessel.refTransform = vessel.referenceTransformId;
            vessel.protoVessel.vesselType = vessel.vesselType;
            vessel.protoVessel.situation = vessel.situation;
            vessel.protoVessel.landed = vessel.Landed;
            vessel.protoVessel.landedAt = vessel.landedAt;
            vessel.protoVessel.displaylandedAt = vessel.displaylandedAt;
            vessel.protoVessel.splashed = vessel.Splashed;
            vessel.protoVessel.vesselName = vessel.vesselName;
            vessel.protoVessel.missionTime = vessel.missionTime;
            vessel.protoVessel.launchTime = vessel.launchTime;
            vessel.protoVessel.lastUT = vessel.lastUT;
            vessel.protoVessel.autoClean = vessel.AutoClean;
            vessel.protoVessel.autoCleanReason = vessel.AutoCleanReason;
            vessel.protoVessel.wasControllable = vessel.IsControllable;

            if (vessel.parts.Count != vessel.protoVessel.protoPartSnapshots.Count)
            {
                vessel.protoVessel.protoPartSnapshots.Clear();
                
                foreach (var part in vessel.parts.Where(p=> p.State != PartStates.DEAD))
                {
                    vessel.protoVessel.protoPartSnapshots.Add(new ProtoPartSnapshot(part, vessel.protoVessel));
                }

                foreach (var part in vessel.protoVessel.protoPartSnapshots)
                {
                    part.storePartRefs();
                }
            }
            else
            {
                RefreshPartModules(vessel);
            }

            if (vessel.crewedParts != vessel.protoVessel.crewedParts || vessel.crewableParts != vessel.protoVessel.crewableParts)
            {
                ((List<ProtoCrewMember>)CrewField.GetValue(vessel.protoVessel)).Clear();
                vessel.protoVessel.RebuildCrewCounts();
            }

            vessel.protoVessel.CoM = vessel.localCoM;
            vessel.protoVessel.latitude = vessel.latitude;
            vessel.protoVessel.longitude = vessel.longitude;
            vessel.protoVessel.altitude = vessel.altitude;
            vessel.protoVessel.height = vessel.heightFromTerrain;
            vessel.protoVessel.normal = vessel.terrainNormal;
            vessel.protoVessel.rotation = vessel.srfRelRotation;
            vessel.protoVessel.stage = vessel.currentStage;
            vessel.protoVessel.persistent = vessel.isPersistent;
            vessel.protoVessel.vesselRef.protoVessel = vessel.protoVessel;
            
            vessel.protoVessel.actionGroups.ClearData();
            vessel.protoVessel.vesselRef.ActionGroups.Save(vessel.protoVessel.actionGroups);
        }

        private static void RefreshPartModules(Vessel vessel)
        {
            for (var i = 0; i < vessel.parts.Count; i++)
            {
                var part = vessel.parts[i];
                if (part.State == PartStates.DEAD) continue;

                for (var j = 0; j < part.Modules.Count; j++)
                {
                    var module = part.Modules[j];
                    module.snapshot.moduleValues.ClearData();
                    module.Save(module.snapshot.moduleValues);
                }
            }
        }
    }
}