/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Linq;
using System.Collections.Generic;
using DOL.Database;

namespace DOL.GS
{
    /// <summary>
    /// MobAmbientBehaviourManager handles Mob Ambient Behaviour Lazy Loading
    /// </summary>
    public sealed class MobAmbientBehaviourManager
    {
        /// <summary>
        /// Mob X Ambient Behaviour Cache indexed by Mob Name
        /// </summary>
        private Dictionary<string, MobXAmbientBehaviour[]> _byName;

        /// <summary>
        /// Retrieve MobXambiemtBehaviour Objects from Mob Name
        /// </summary>
        public MobXAmbientBehaviour[] this[string index]
            => !string.IsNullOrEmpty(index) && _byName.TryGetValue(index.ToLowerInvariant(), out var val)
                ? val
                : Array.Empty<MobXAmbientBehaviour>();

        /// <summary>
        /// Call it after delete or add a trigger
        /// </summary>
        public void Reload(IObjectDatabase db)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            var next = db.SelectAllObjects<MobXAmbientBehaviour>()
                .GroupBy(x => x.Source)
                .ToDictionary(g => g.Key.ToLowerInvariant(), g => g.ToArray());
            _byName = next;
        }

        /// <summary>
        /// Create a new Instance of <see cref="MobAmbientBehaviourManager"/>
        /// </summary>
        public MobAmbientBehaviourManager(IObjectDatabase db)
        {
            Reload(db);
        }
    }
}
