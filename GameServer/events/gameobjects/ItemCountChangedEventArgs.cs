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
using DOL.Database;
using DOL.GS;

namespace DOL.Events
{
    /// <summary>
    /// Holds the arguments for PlayerInventoryEvent.ItemCountChanged
    /// </summary>
    public class ItemCountChangedEventArgs : EventArgs
    {
        public GamePlayer Player { get; }
        public InventoryItem Item { get; }
        /// <summary>
        /// Change in count. &gt; 0 = gained, &lt; 0 = lost.
        /// </summary>
        public int Delta { get; }

        public ItemCountChangedEventArgs(GamePlayer player, InventoryItem item, int delta)
        {
            Player = player;
            Item = item;
            Delta = delta;
        }
    }
}