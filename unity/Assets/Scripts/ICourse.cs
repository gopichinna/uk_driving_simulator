using System;
using System.Collections.Generic;
using LToLocal.Rules;
using LToLocal.Sim;
using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// A practice area: its own traffic and its own rules. The player's car, cameras, mirrors, controls,
    /// speed rule and feedback live in <see cref="DriveGame"/> and are shared by every course.
    /// </summary>
    public interface ICourse
    {
        string DisplayName { get; }
        CarState StartState { get; }
        double SpeedLimitMph(in CarState player);
        bool OutOfBounds(in CarState player);
        void ResetCourse();
        /// <summary>Snapshot vehicles before a step (for render interpolation).</summary>
        void CapturePrevious();
        void Step(double dt, in CarState player, double simTime, int indicator, List<RuleEvent> events);
        void Render(float alpha, bool blink);
        /// <summary>Other road users, for rules shared by all courses (e.g. passing cyclists).</summary>
        void GetRoadUsers(List<RoadUser> into);
        /// <summary>Everyone's pose (and the traffic lights) right now, for the drive recording.</summary>
        void Record(List<ActorPose> actors, List<LightState> lights);
        /// <summary>Shows a recorded moment instead of the live simulation (debrief replays).</summary>
        void ShowRecorded(RecordedFrame frame, bool blink);
    }
}
