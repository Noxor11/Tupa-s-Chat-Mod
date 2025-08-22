using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;



namespace ChatMod {
    public class CustomChat {

        public static ICoreClientAPI clientAPI {
            get {return ChatModModSystem.clientAPI;}
        }

        private static bool isChannelLocked = false;

        static HudElement Chat { get {return ChatModModSystem.chat;} }

        public static void OnTabClicked(int groupId) {
            Chat.GetType().GetMethod("OnTabClicked", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Chat, new object[] { groupId });
        }

        public static void ToggleChannelLocked(){
            isChannelLocked = !isChannelLocked;
            clientAPI.ShowChatMessage(Lang.Get("chatmod:channel-lock-status") + " " + (isChannelLocked ? Lang.Get("chatmod:locked") : Lang.Get("chatmod:unlocked")));
        }

        private static bool HandleGotoGroupPacket(HudElement __instance, object packet){
            return !isChannelLocked; // if true it goes on and handles the packet (moves to the group / channel)
        }

        /**
            Because we are checking for keypresses
            while focused on the chat, we can't use
            the api to register the hotkeys.
        */
        public static void OnKeyDown(KeyEvent args){
            if (!Chat.IsOpened())
                return;

            if (!args.AltPressed)
                return;
                
            // check for number press
            for (int numberKeyPressed = (int)GlKeys.Number1; numberKeyPressed <= (int)GlKeys.Number9; numberKeyPressed++) {
                if (args.KeyCode == numberKeyPressed){
                    GuiTab[] tabs = (GuiTab[])Chat.GetType().GetField("tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Chat);
                    int actualNumber = numberKeyPressed - (int)GlKeys.Number1; // decreased by one such that General Chat (position 0) is accessed with the number one
                    int? groupId = tabs.ElementAtOrDefault(actualNumber)?.DataInt;
                    if (groupId.HasValue){
                        GuiElementHorizontalTabs gui_tabs = (GuiElementHorizontalTabs)Chat.Composers["chat"].GetElement("tabs");
                        OnTabClicked(groupId.Value);
                        gui_tabs.activeElement = actualNumber;
                        args.Handled = true;
                        return;
                    } 
                }
            }

            // Starting from version 1.20.0, the chat keeps focus on the current tab.
            if (GameVersion.IsLowerVersionThan(GameVersion.APIVersion, "1.20.0")) {
                if (args.KeyCode == (int)GlKeys.L){
                    ToggleChannelLocked();
                }
            }

            return;
        }

        /**
            We'll use essentially the original code, except for a small change that checks
            whether the channel in which a message was received was locked or not.
        */
        public static bool OnFinalizeFrame(float dt){ 
            // TODO: handle this in a way that won't affect future changes of the game's code
            const int currentChatGroupId = -2;

            foreach (KeyValuePair<string, GuiComposer> val in Chat.Composers)
				val.Value.PostRender(dt);
			
			if (Chat.Focused)
                Chat.GetType().GetField("lastActivityMs", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Chat, clientAPI.ElapsedMilliseconds);
			
			if (clientAPI.Settings.Bool["AutoChat"]){
                long lastActivityMs = (long)Chat.GetType().GetField("lastActivityMs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Chat);
				if (Chat.IsOpened() && clientAPI.ElapsedMilliseconds - lastActivityMs > 15000L)
                    Chat.GetType().GetMethod("DoClose").Invoke(Chat, null);
				
				if (!Chat.IsOpened() && clientAPI.ElapsedMilliseconds - lastActivityMs < 50L) {
					Chat.TryOpen();
                    int lastMessageInGroupId = (int)Chat.GetType().GetField("lastMessageInGroupId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Chat);
                    if (lastMessageInGroupId > -99) {
                        object game = Chat.GetType().GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Chat);
                        int groupId = (int)lastMessageInGroupId;
                        if (groupId == currentChatGroupId)
                            groupId = (int)game.GetType().GetField("currentGroupid").GetValue(game);
                        
                        int tabIndex = (int)Chat.GetType().GetMethod("tabIndexByGroupId", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Chat, new object[]{groupId});
                        if (!isChannelLocked || lastMessageInGroupId == currentChatGroupId) {
                            // the channel is not locked, or the current channel is the one that's locked.
                            // we'll just do the same thing as the original code
                            if (tabIndex >= 0)
                                Chat.Composers["chat"].GetHorizontalTabs("tabs").SetValue(tabIndex, false);
                            
                            Chat.Composers["chat"].GetHorizontalTabs("tabs").SetAlarmTab(-1);
                            game.GetType().GetField("currentGroupid").SetValue(game, groupId);

                            Dictionary<int, bool> unreadByGroupId = (Dictionary<int, bool>)Chat.GetType().GetField("unreadByGroupId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Chat);
                            unreadByGroupId[groupId] = false;
                            Chat.GetType().GetMethod("UpdateText", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Chat, null);
                        } else { // the channel is locked, and the message that we just received does not correspond to the current channel
                            if (tabIndex >= 0) {
                                GuiElementHorizontalTabs tab = Chat.Composers["chat"].GetHorizontalTabs("tabs");
                                int lastIndex = tab.TabIndex;
                                tab.SetAlarmTab(-1); // we mark there's a new message in the tab 
                                tab.SetValue(lastIndex); // we set the current tab as active again
                            }
                            Dictionary<int, bool> unreadByGroupId = (Dictionary<int, bool>)Chat.GetType().GetField("unreadByGroupId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Chat);
                            unreadByGroupId[groupId] = false;
                            Chat.GetType().GetMethod("UpdateText", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Chat, null);
                            
                            /* 
                                We close the chat as we don't want to see new messages in other channels.
                                We do it in the same frame, so it won't show up.
                                Openning it is necessary for the message to register.
                            */
                            Chat.GetType().GetMethod("DoClose").Invoke(Chat, null); 
                        }
                    }
				}
			}
            return false;
        }

        public static void OnNewServerToClientChatLine(int groupId, ref string message, EnumChatType chattype, string data) {
            StringBuilder stringBuilder = new();
            foreach (var word in message.Split(" ")){
                // let's check if it's a link 
                if (word.StartsWith("http://") || word.StartsWith("https://") || word.StartsWith("handbook://")){
                    string parsedLink = "<a href=\"" + word + "\" >" + word + "</a>";
                    stringBuilder.Append(parsedLink + " ");
                } else
                    stringBuilder.Append(word + " ");
            }
            message = stringBuilder.ToString().Trim();
        }

        public static void OnNewClientToServerChatLine(int groupId, ref string message, EnumChatType chattype, string data) {
            CurrentItemToHandbookURI(ref message);
        }

        /**
            The following code belongs to William Blake Galbreath, with MIT License, and it contains 
            slight modifications to allow for client side resolution of the current item,
            as well as hyperlink rendering on client side only.
        */
        [SuppressMessage("GeneratedRegex", "SYSLIB1045:Convert to \'GeneratedRegexAttribute\'.")]
        private static readonly Regex ITEM_LINK = new(@"(\[item\])", RegexOptions.IgnoreCase);
        private static void CurrentItemToHandbookURI(ref string message) {
            MatchCollection matches = ITEM_LINK.Matches(message);
            if (matches.Count == 0) {
                return;
            }

            int slotNum = clientAPI.World.Player.InventoryManager.ActiveHotbarSlotNumber;
            ItemStack itemStack = clientAPI.World.Player.InventoryManager.GetHotbarItemstack(slotNum);
            string pageCode = itemStack == null ? "" : GuiHandbookItemStackPage.PageCodeForStack(itemStack);

            string replacement = $"{(pageCode is { Length: > 0 } ?
                $"{itemStack!.GetName()}: handbook://{pageCode}" :
                itemStack?.GetName() ?? Lang.Get("game:nothing"))}";
            
            foreach (Match match in matches) {
                message = message.Replace(match.Value, replacement);
        }
    }
    }
}