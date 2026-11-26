using System.Collections.Generic;
using LToLocal.Sim;

namespace LToLocal.Game
{
    /// <summary>Converts between live vehicle snapshots and recorded poses, for every course.</summary>
    public static class Replay
    {
        public static void AddVehicles(List<ActorPose> into, List<VehicleSnapshot> vehicles)
        {
            foreach (var v in vehicles)
                into.Add(new ActorPose
                {
                    Id = v.Id, Kind = v.IsCyclist ? ActorKind.Cyclist : ActorKind.Car,
                    X = (float)v.X, Z = (float)v.Z, Heading = (float)v.Heading,
                    Indicator = (sbyte)v.Indicator, Colour = (byte)v.Colour,
                });
        }

        public static void Vehicles(RecordedFrame f, List<VehicleSnapshot> into)
        {
            into.Clear();
            foreach (var a in f.Actors)
                if (a.Kind != ActorKind.Pedestrian)
                    into.Add(new VehicleSnapshot { Id = a.Id, X = a.X, Z = a.Z, Heading = a.Heading, Indicator = a.Indicator, Colour = a.Colour, IsCyclist = a.Kind == ActorKind.Cyclist });
        }
    }
}
