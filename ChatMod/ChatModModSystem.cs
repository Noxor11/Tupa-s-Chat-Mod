using Vintagestory.API.Common;
using Vintagestory.API.Client;
using System;
using System.Linq;
using HarmonyLib;
using System.Runtime.InteropServices;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.API.Common.CommandAbbr;

namespace ChatMod
{
    
    public class ChatModModSystem : ModSystem
    {

	    public const string ModId = "tupaschatmod";
        static public ICoreClientAPI clientAPI;
        static public HudElement chat;        
        static Harmony harmony;
        // public override bool AllowRuntimeReload => true; // not existing in 1.19.3
        public override bool ShouldLoad(EnumAppSide side) => side.IsClient();

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            harmony = new Harmony("tupaschatmod");
            clientAPI = api;

            FindChat();
            Patch(harmony.Id);
        }

        private void FindChat() {
            foreach (object obj in clientAPI.Gui.LoadedGuis){
                if (obj.GetType().ToString() == "Vintagestory.Client.NoObf.HudDialogChat"){
                    chat = (HudElement)obj;
                    return;
                }   
            }
        }

        public static void Patch(string harmonyId) {
            new Harmony(harmonyId).Patch(
                AccessTools.Method(chat.GetType(), "HandleGotoGroupPacket"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CustomChat), "HandleGotoGroupPacket"))
            );

            new Harmony(harmonyId).Patch(
                AccessTools.Method(chat.GetType(), "OnKeyDown"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CustomChat), "OnKeyDown"))
            );

            new Harmony(harmonyId).Patch(
                AccessTools.Method(chat.GetType(), "OnFinalizeFrame"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CustomChat), "OnFinalizeFrame"))
            );

            new Harmony(harmonyId).Patch(
                AccessTools.Method(chat.GetType(), "OnNewServerToClientChatLine"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CustomChat), "OnNewServerToClientChatLine"))
            );       
            
            new Harmony(harmonyId).Patch(
                AccessTools.Method(chat.GetType(), "OnNewClientToServerChatLine"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CustomChat), "OnNewClientToServerChatLine"))
            );       
        }
    }
}
