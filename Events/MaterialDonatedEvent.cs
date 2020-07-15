﻿using EddiDataDefinitions;
using System;
using System.Collections.Generic;
using Utilities;

namespace EddiEvents
{
    public class MaterialDonatedEvent : Event
    {
        public const string NAME = "Material donated";
        public const string DESCRIPTION = "Triggered when you donate a material";
        public const string SAMPLE = "{ \"timestamp\":\"2016-10-05T11:32:57Z\", \"event\":\"ScientificResearch\", \"Name\":\"nickel\", \"Category\":\"Raw\", \"Count\":5, \"MarketID\": 128666762 }";

        public static Dictionary<string, string> VARIABLES = new Dictionary<string, string>();

        static MaterialDonatedEvent()
        {
            VARIABLES.Add("name", "The name of the donated material");
            VARIABLES.Add("amount", "The amount of the donated material");
        }

        public string name { get; private set; }

        public int amount { get; private set; }

        // Not intended to be user facing

        [VoiceAttackIgnore]
        public string edname { get; private set; }

        [VoiceAttackIgnore]
        public long marketId { get; private set; }

        public MaterialDonatedEvent(DateTime timestamp, Material material, int amount, long marketId) : base(timestamp, NAME)
        {
            this.name = material?.localizedName;
            this.amount = amount;
            this.edname = material?.edname;
            this.marketId = marketId;
        }
    }
}
