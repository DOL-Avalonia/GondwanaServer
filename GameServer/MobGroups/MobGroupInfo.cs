﻿using DOL.GS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DOL.GS.GameNPC;

namespace DOL.MobGroups
{
    public class MobGroupInfo
    {
        public bool? IsInvincible
        {
            get;
            set;
        }

        public long? Flag
        {
            get;
            set;
        }

        public byte? VisibleSlot
        {
            get;
            set;
        }

        public eRace? Race
        {
            get;
            set;
        }

        public int? Model
        {
            get;
            set;
        }

        public int? Effect
        {
            get;
            set;
        }
    }
}