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
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System.Threading.Tasks;

namespace DOL.GS
{
    public enum eNewsType : byte
    {
        RvRGlobal = 0,
        RvRLocal = 1,
        PvE = 2,
    }

    public class NewsMgr
    {
        public static void CreateNews(IDictionary<string, string> translations, eRealm realm, eNewsType type, bool sendMessage, params object[] args)
        {
            if (sendMessage)
            {
                foreach (GameClient client in WorldMgr.GetAllClients())
                {
                    if (client.Player == null)
                        continue;

                    if (client.Account.PrivLevel <= 1 && realm != eRealm.None && client.Player.Realm != realm && !Properties.SERVER_IS_CROSS_REALM)
                        continue;

                    string lang = client.Player.Client.Account.Language;
                    string translated = null;
                    if (!translations.TryGetValue(lang, out translated))
                    {
                        lang = Properties.SERV_LANGUAGE;
                        if (!translations.TryGetValue(lang, out translated))
                        {
                            continue;
                        }
                    }
                    if (!string.IsNullOrEmpty(translated))
                        client.Out.SendMessage(translated, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            if (ServerProperties.Properties.RECORD_NEWS)
            {
                DBNews news = new DBNews();
                news.Type = (byte)type;
                news.Realm = (byte)realm;
                string str;
                if (translations.TryGetValue(LanguageMgr.ENGLISH, out str))
                    news.Text = str;
                if (translations.TryGetValue(LanguageMgr.FRENCH, out str))
                    news.TextFR = str;
                GameServer.Database.AddObject(news);
                GameEventMgr.Notify(DatabaseEvent.NewsCreated, new NewsEventArgs(news));
            }
        }

        public static Task CreateNews(KeyTranslator translator, eRealm realm, eNewsType type, bool sendMessage, params object[] args)
        {
            List<Task> allTasks = new();
            if (sendMessage)
            {
                allTasks.AddRange(WorldMgr.GetAllClients().Select(async client =>
                {
                    if (client.Player == null)
                        return;

                    if (client.Account.PrivLevel <= 1 && realm != eRealm.None && client.Player.Realm != realm && !Properties.SERVER_IS_CROSS_REALM)
                        return;

                    var translated = await translator.Translate(client.Player, args);

                    if (!string.IsNullOrEmpty(translated))
                        client.Out.SendMessage(translated, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }));
            }

            if (ServerProperties.Properties.RECORD_NEWS)
            {
                allTasks.Add(Task.Run(async () =>
                {
                    DBNews news = new DBNews();
                    news.Type = (byte)type;
                    news.Realm = (byte)realm;
                    string str;
                    str = await translator.Translate(LanguageMgr.ENGLISH, Properties.AUTOTRANSLATE_ENABLE, args);
                    if (!string.IsNullOrEmpty(str))
                        news.Text = str;
                    str = await translator.Translate(LanguageMgr.FRENCH, Properties.AUTOTRANSLATE_ENABLE, args);
                    if (!string.IsNullOrEmpty(str))
                        news.TextFR = str;
                    GameServer.Database.AddObject(news);
                    GameEventMgr.Notify(DatabaseEvent.NewsCreated, new NewsEventArgs(news));
                }));
            }

            return Task.WhenAll(allTasks);
        }
        
        public static void CreateNews(string message, eRealm realm, eNewsType type, bool sendMessage, bool translate = false, params object[] args)
        {
            if (translate)
            {
                var translations = LanguageMgr.GetAllTranslations(message, args);
                CreateNews(translations, realm, type, sendMessage, args);
            }
            else
            {
                CreateNews(new Dictionary<string, string>(){ { LanguageMgr.ENGLISH, message } }, realm, type, sendMessage, args);
            }
        }
        
        public static void CreateNews(Func<string, string> messageSupplier, eRealm realm, eNewsType type, bool sendMessage, params object[] args)
        {
            var dict = new Dictionary<string, string>();
            foreach (string lang in LanguageMgr.GetAllSupportedLanguages())
            {
                string translation = messageSupplier(lang);
                if (!string.IsNullOrEmpty(translation))
                    dict[lang] = translation;
            }
        }

        public static void DisplayNews(GameClient client)
        {
            // N,chanel(0/1/2),index(0-4),string time,\"news\"

            for (int type = 0; type <= 2; type++)
            {
                int index = 0;
                string realm = "";
                //we can see all captures
                IList<DBNews> newsList;
                if (type > 0 && !ServerProperties.Properties.SERVER_IS_CROSS_REALM)
                    newsList = DOLDB<DBNews>.SelectObjects(DB.Column(nameof(DBNews.Type)).IsEqualTo(type).And(DB.Column(nameof(DBNews.Realm)).IsEqualTo(0).Or(DB.Column(nameof(DBNews.Realm)).IsEqualTo(realm))));
                else
                    newsList = DOLDB<DBNews>.SelectObjects(DB.Column(nameof(DBNews.Type)).IsEqualTo(type));

                newsList = newsList.OrderByDescending(it => it.CreationDate).Take(5).ToArray();
                int n = newsList.Count;

                while (n > 0)
                {
                    n--;
                    DBNews news = newsList[n];
                    if (client.Account.Language == "FR" && !string.IsNullOrEmpty(news.TextFR))
                        client.Out.SendMessage(string.Format("N,{0},{1},{2},\"{3}\"", news.Type, index++, RetElapsedTime(news.CreationDate), news.TextFR), eChatType.CT_SocialInterface, eChatLoc.CL_SystemWindow);
                    else
                        client.Out.SendMessage(string.Format("N,{0},{1},{2},\"{3}\"", news.Type, index++, RetElapsedTime(news.CreationDate), news.Text), eChatType.CT_SocialInterface, eChatLoc.CL_SystemWindow);
                }
            }
        }

        private static string RetElapsedTime(DateTime dt)
        {
            TimeSpan playerEnterGame = DateTime.Now.Subtract(dt);
            string newsTime;
            if (playerEnterGame.Days > 0)
                newsTime = playerEnterGame.Days.ToString() + " day" + ((playerEnterGame.Days > 1) ? "s" : "");
            else if (playerEnterGame.Hours > 0)
                newsTime = playerEnterGame.Hours.ToString() + " hour" + ((playerEnterGame.Hours > 1) ? "s" : "");
            else if (playerEnterGame.Minutes > 0)
                newsTime = playerEnterGame.Minutes.ToString() + " minute" + ((playerEnterGame.Minutes > 1) ? "s" : "");
            else
                newsTime = playerEnterGame.Seconds.ToString() + " second" + ((playerEnterGame.Seconds > 1) ? "s" : "");
            return newsTime;
        }
    }
}