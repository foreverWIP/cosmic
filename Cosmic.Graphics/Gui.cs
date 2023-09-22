using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Cosmic.Core;
using ImGuiNET;
using static Cosmic.Core.Collision;
using static Cosmic.Core.Drawing;
using static Cosmic.Core.EngineStuff;
using static Cosmic.Core.Input;
using static Cosmic.Core.Stage;

namespace Cosmic.Graphics;

public sealed partial class Renderer
{
    Vector2 menuBarSize = Vector2.Zero;

    static unsafe byte* UtilAllocate(int byteCount) => (byte*)Marshal.AllocHGlobal(byteCount);

    static unsafe void ImGuiUtilFree(byte* ptr) => Marshal.FreeHGlobal((IntPtr)ptr);

    static unsafe int ImGuiUtilGetUtf8(string s, byte* utf8Bytes, int utf8ByteCount)
    {
        fixed (char* utf16Ptr = s)
        {
            return Encoding.UTF8.GetBytes(utf16Ptr, s.Length, utf8Bytes, utf8ByteCount);
        }
    }

    unsafe static bool ImGuiBeginPopupModal(string name, bool* p_open, ImGuiWindowFlags flags)
    {
        int num = 0;
        byte* ptr;
        if (name != null)
        {
            num = Encoding.UTF8.GetByteCount(name);
            // ptr = ((num <= 2048) ? stackalloc byte[(int)(uint)(num + 1)] : UtilAllocate(num + 1));
            ptr = UtilAllocate(num + 1);
            int utf = ImGuiUtilGetUtf8(name, ptr, num);
            ptr[utf] = 0;
        }
        else
        {
            ptr = null;
        }
        byte num2;
        if (p_open != null)
        {
            byte b = (byte)(*p_open ? 1 : 0);
            byte* p_open2 = &b;
            num2 = ImGuiNative.igBeginPopupModal(ptr, p_open2, flags);
            // if (num > 2048)
            {
                ImGuiUtilFree(ptr);
            }
            *p_open = b != 0;
        }
        else
        {
            num2 = ImGuiNative.igBeginPopupModal(ptr, null, flags);
            // if (num > 2048)
            {
                ImGuiUtilFree(ptr);
            }
        }
        return num2 != 0;
    }

    unsafe void DoGUIMenus(ref bool inputDisplay)
    {
        if (ImGui.BeginMainMenuBar())
        {
            menuBarSize = ImGui.GetWindowSize();

            var optionsOpen = false;
            var aboutOpen = false;
            if (ImGui.BeginMenu("Game"))
            {
                optionsOpen = ImGui.MenuItem("Options...");
                aboutOpen = ImGui.MenuItem("About");

                ImGui.EndMenu();
            }

            if (optionsOpen)
            {
                ImGui.OpenPopup("Options");
            }

            if (aboutOpen)
            {
                ImGui.OpenPopup("About Game");
            }

            var mainFbSize = GetMainFramebufferSize();
            var winSize = new Vector2(mainFbSize.w, mainFbSize.h - menuBarSize.Y);
            ImGui.SetNextWindowPos(new Vector2(winSize.X / 2, (winSize.Y + menuBarSize.Y) / 2), ImGuiCond.Always, new Vector2(0.5f));
            if (ImGuiBeginPopupModal("Options", null, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("Graphics");
                {
                    var defaultGameViewScaleTmp = 0;
                    if (ImGui.InputInt("Window Scale", ref defaultGameViewScaleTmp))
                    {
                        var gameViewScale = Math.Clamp(defaultGameViewScaleTmp, 1, 5);
                        SetScreenDimensions(SCREEN_XSIZE * gameViewScale, SCREEN_YSIZE * gameViewScale + (int)menuBarSize.Y);
                    }
                    var render2xTmp = BaseRenderScale == 2;
                    ImGui.Checkbox("Render 2x", ref render2xTmp);
                    BaseRenderScale = (uint)(render2xTmp ? 2 : 1);
                    var tmpVsync = false;
                    ImGui.Checkbox("VSync", ref tmpVsync);
                    VSync = tmpVsync;
                }

                ImGui.Text("Audio");
                {
                    ImGui.SliderInt("Music Volume", ref Audio.bgmVolume, 0, 100, "%i%%", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.SliderInt("Sound Effect Volume", ref Audio.sfxVolume, 0, 100, "%i%%", ImGuiSliderFlags.AlwaysClamp);
                }

                ImGui.Text("Tools");
                {
                    ImGui.Checkbox("Input Display", ref inputDisplay);
                }

                if (ImGui.Button("OK"))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.SetNextWindowPos(new Vector2(winSize.X / 2, (winSize.Y + menuBarSize.Y) / 2), ImGuiCond.Always, new Vector2(0.5f));
            if (ImGuiBeginPopupModal("About Game", null, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text(gameWindowText);
                foreach (var line in gameDescriptionText.Split('\r'))
                {
                    ImGui.Text(line);
                }
                ImGui.Text(gameVersion);

                if (ImGui.Button("OK"))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (platform.gameDebugMode)
            {
                if (ImGui.BeginMenu("Debug"))
                {
                    if (ImGui.MenuItem("Restart Game", "F1"))
                    {
                        platform.ClearError();
                        ResetSceneResources();
                        Engine.RestartGame();
                    }

                    if (ImGui.BeginMenu("Display"))
                    {
                        ImGui.Checkbox("Show Hitboxes", ref showHitboxes);
                        ImGui.Checkbox("Show Touch Regions", ref showTouches);

                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem("Restart Scene", "F5"))
                    {
                        platform.ClearError();
                        ResetSceneResources();
                        Engine.RestartScene();
                    }

                    if (ImGui.BeginMenu("Change Player"))
                    {
                        var playerNames = CosmicEngine.GetPlayerNames();
                        for (var i = 0; i < playerNames.Length; i++)
                        {
                            if (ImGui.MenuItem(playerNames[i]))
                            {
                                platform.ClearError();
                                CosmicEngine.SetPlayer(i);
                                ResetCurrentStageFolder(); // reload all assets & scripts
                                stageMode = StageModes.STAGEMODE_LOAD;
                            }
                        }

                        ImGui.EndMenu();
                    }

                    if (ImGui.BeginMenu("Change Scene"))
                    {
                        for (var c = 0; c < (int)StageListNames.STAGELIST_MAX; c++)
                        {
                            if (ImGui.BeginMenu(stageListNames[c]))
                            {
                                for (var s = 0; s < stageListCount[c]; s++)
                                {
                                    if (ImGui.MenuItem(stageList[c, s].name))
                                    {
                                        platform.ClearError();
                                        activeStageList = (StageListNames)c;
                                        stageListPosition = s;
                                        stageMode = StageModes.STAGEMODE_LOAD;
                                        gameMode = EngineStates.ENGINE_MAINGAME;
                                        Cosmic.Core.Script.SetGlobalVariableByName("LampPost.Check", 0);
                                        Cosmic.Core.Script.SetGlobalVariableByName("Warp.XPos", 0);
                                    }
                                }

                                ImGui.EndMenu();
                            }
                        }

                        ImGui.EndMenu();
                    }

                    ImGui.EndMenu();
                }
            }

            ImGui.SameLine(menuBarSize.X - ImGui.CalcTextSize(CosmicID).X - 10);
            ImGui.Text(CosmicID);

            ImGui.EndMainMenuBar();
        }
    }

    void DoGUI(ref bool inputDisplay)
    {
        var rootWindowSize = ImGui.GetWindowSize();

        DoGUIMenus(ref inputDisplay);

        var winPos = Vector2.Zero;
        var mainFbSize = GetMainFramebufferSize();
        var winSize = new Vector2(mainFbSize.w, mainFbSize.h - menuBarSize.Y);

        float outputAspect = winSize.X / winSize.Y;
        const float preferredAspect = SCREEN_XSIZE / (float)SCREEN_YSIZE;
        if (outputAspect <= preferredAspect)
        {
            // output is taller than it is wider, bars on top/bottom
            int presentHeight = (int)((winSize.X / preferredAspect) + 0.5f);
            int barHeight = (int)((winSize.Y - presentHeight) / 2);
            winPos.Y = barHeight;
            winSize.Y = presentHeight;
        }
        else
        {
            // output is wider than it is tall, bars left/right
            int presentWidth = (int)((winSize.Y * preferredAspect) + 0.5f);
            int barWidth = (int)((winSize.X - presentWidth) / 2);
            winPos.X = barWidth;
            winSize.X = presentWidth;
        }
        winPos.Y += menuBarSize.Y;

        if (!platform.ErrorStop)
        {
            ImGui.SetNextWindowSize(winSize, ImGuiCond.Always);
            ImGui.SetNextWindowPos(winPos, ImGuiCond.Always);
            {
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            }
            if (ImGui.Begin(
                "MainGameDisplay",
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoBringToFrontOnFocus
            ))
            {
                var uv1 = Vector2.One;
                if (BaseRenderScale == 1)
                {
                    uv1 /= 2;
                }
                ImGui.Image(GetImGuiGameViewTexture(), winSize, Vector2.Zero, uv1);

                ImGui.End();
            }
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
            }

            if (inputDisplay)
            {
                const string fullString = "ABCS ←↑↓→";
                ImGui.SetNextWindowPos(new Vector2(0, menuBarSize.Y));
                ImGui.SetNextWindowSizeConstraints(ImGui.CalcTextSize(fullString), Vector2.One * 99999);
                if (ImGui.Begin("Input Display",
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoFocusOnAppearing |
                    ImGuiWindowFlags.NoNavFocus |
                    ImGuiWindowFlags.NoDecoration
                ))
                {
                    ImGui.Text($"{(keyDown[0].A ? "A" : " ")}{(keyDown[0].B ? "B" : " ")}{(keyDown[0].C ? "C" : " ")}{(keyDown[0].start ? "S" : " ")} {(keyDown[0].left ? "←" : " ")}{(keyDown[0].up ? "↑" : " ")}{(keyDown[0].down ? "↓" : " ")}{(keyDown[0].right ? "→" : " ")}");

                    ImGui.End();
                }
            }
            /*else
            {
                ImGui.SetNextWindowPos(new Vector2(0, menuBarSize.Y));
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                if (ImGui.Begin("Floor Debug",
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoFocusOnAppearing |
                    ImGuiWindowFlags.NoNavFocus |
                    ImGuiWindowFlags.NoDecoration
                ))
                {
                    ImGui.SliderInt("X", ref DrawLayers.Floor3DX, 0, 4096 * 0x1_0000);
                    ImGui.SliderInt("Y", ref DrawLayers.Floor3DY, 0, 4096 * 0x1_0000);
                    ImGui.SliderInt("Z", ref DrawLayers.Floor3DZ, 0, 4096 * 0x1_0000);
                    ImGui.SliderInt("Angle", ref DrawLayers.Floor3DAngle, 0, 512);
                }
                ImGui.PopStyleVar();
            }*/
        }
        else
        {
            {
                ImGui.PushStyleVar(ImGuiStyleVar.WindowTitleAlign, new Vector2(0.5f, 0.5f));
            }
            const string errorWindowText = "Error reported from game: ";
            ImGui.CalcTextSize(errorWindowText);
            ImGui.SetNextWindowPos(new Vector2(winSize.X / 2, (winSize.Y + menuBarSize.Y) / 2), ImGuiCond.Always, new Vector2(0.5f));
            ImGui.SetNextWindowSizeConstraints(new Vector2(ImGui.CalcTextSize(errorWindowText).X, 0), new Vector2(99999));
            if (ImGui.Begin(
                errorWindowText,
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoFocusOnAppearing
            ))
            {
                unsafe
                {
                    for (var i = 0; i < Text.gameMenu[0].rowCount; i++)
                    {
                        fixed (ushort* dataPtr = Text.gameMenu[0].textData)
                        {
                            ImGui.Text(new string((char*)(dataPtr + Text.gameMenu[0].entryStart[i]), 0, Text.gameMenu[0].entrySize[i] * sizeof(ushort)));
                        }
                    }
                }

                ImGui.End();
            }
            {
                ImGui.PopStyleVar();
            }
        }

        // ImGui.ShowDemoWindow();
    }
}