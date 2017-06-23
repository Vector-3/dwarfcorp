﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gum;

namespace DwarfCorp.Goals.Goals
{
    public class Ordu_Start : Goal
    {
        public Ordu_Start()
        {
            Name = "Ordu: Uzzikal the Necromancer";
            Description = "Uzzikal, king of the Necromancers of Ordu, wishes to trade with you.";
            GoalType = GoalTypes.UnavailableAtStartup;
        }

        public override ActivationResult Activate(WorldManager World)
        {
            // Create Ordu faction, add to FactionLibrary and World.Natives
            var orduFaction = new Faction(World)
            {
                Race = World.ComponentManager.Factions.Races["Undead"],
                Name = "Ordu",
                PrimaryColor = new HSLColor(300.0, 100.0, 100.0),
                SecondaryColor = new HSLColor(300.0, 50.0, 50.0),
                TradeMoney = (decimal)1000.0f,
                Center = new Microsoft.Xna.Framework.Point(MathFunctions.RandInt(0, Overworld.Map.GetLength(0)), MathFunctions.RandInt(0, Overworld.Map.GetLength(1)))
            };

            World.ComponentManager.Factions.Factions.Add("Ordu", orduFaction);
            World.Natives.Add(orduFaction);
            World.ComponentManager.Diplomacy.InitializeFactionPolitics(orduFaction, World.Time.CurrentDate);

            // Spawn trade convoy from Ordu
            World.ComponentManager.Diplomacy.SendTradeEnvoy(orduFaction, World);

            return new ActivationResult { Succeeded = true };
        }

        public override void OnGameEvent(WorldManager World, GameEvent Event)
        {
            var trade = Event as Events.Trade;
            if (trade != null)
            {
                if (trade.OtherFaction.Name == "Ordu")
                {
                    State = GoalState.Complete;
                    World.MakeAnnouncement("Goal complete. Traded with Ordu.");
                    World.GoalManager.TryActivateGoal(World, World.GoalManager.FindGoal(typeof(Ordu_Bullion)));
                }
            }
        }
    }
}