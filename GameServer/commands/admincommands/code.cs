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
using System.Reflection;
using System.Text;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [Cmd(
        "&code",
        ePrivLevel.Admin,
        "AdminCommands.Code.Description",
        "AdminCommands.Code.Usage")]
    public class DynCodeCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public static void ExecuteCode(GameClient client, string methodBody)
        {
            var compiler = new DOLScriptCompiler();
            var compiledAssembly = compiler.CompileFromText(client, GetCode(methodBody));

            var methodinf = compiledAssembly.GetType("DynCode")!.GetMethod("DynMethod");

            try
            {
                methodinf!.Invoke(null, new object[] { client.Player == null ? null : client.Player.TargetObject, client.Player });

                if (client.Player != null)
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "AdminCommands.Code.CodeExecuted"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    log.Debug("Code Executed.");
                }

            }
            catch (Exception ex)
            {
                if (client.Player != null)
                {
                    string[] errors = ex.ToString().Split('\n');
                    foreach (string error in errors)
                        client.Out.SendMessage(error, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    log.Debug("Error during execution.");
                }
            }
        }

        private static string GetCode(string methodBody)
        {
            StringBuilder text = new StringBuilder();
            text.Append("using System;\n");
            text.Append("using System.Reflection;\n");
            text.Append("using System.Collections;\n");
            text.Append("using System.Threading;\n");
            text.Append("using DOL;\n");
            text.Append("using DOL.AI;\n");
            text.Append("using DOL.AI.Brain;\n");
            text.Append("using DOL.Database;\n");
            text.Append("using DOL.GS;\n");
            text.Append("using DOL.GS.Movement;\n");
            text.Append("using DOL.GS.Housing;\n");
            text.Append("using DOL.GS.Keeps;\n");
            text.Append("using DOL.GS.Quests;\n");
            text.Append("using DOL.GS.Commands;\n");
            text.Append("using DOL.GS.Scripts;\n");
            text.Append("using DOL.GS.PacketHandler;\n");
            text.Append("using DOL.Events;\n");
            text.Append("using log4net;\n");
            text.Append("public class DynCode {\n");
            text.Append("private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);\n");
            text.Append("public static GameClient Client = null;\n");
            text.Append("public static void print(object obj) {\n");
            text.Append("	string str = (obj==null)?\"(null)\":obj.ToString();\n");
            text.Append("	if (Client==null || Client.Player==null) Log.Debug(str);\n	else Client.Out.SendMessage(str, eChatType.CT_System, eChatLoc.CL_SystemWindow);\n}\n");
            text.Append("public static void DynMethod(GameObject target, GamePlayer player) {\nif (player!=null) Client = player.Client;\n");
            text.Append("GameNPC targetNpc = target as GameNPC;");
            text.Append(methodBody);
            text.Append("\n}\n}\n");
            return text.ToString();
        }

        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }
            string code = String.Join(" ", args, 1, args.Length - 1);
            ExecuteCode(client, code);
        }
    }
}
