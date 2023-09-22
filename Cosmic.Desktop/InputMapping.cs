using System.Collections.Generic;
using Cosmic.Core;
using static Cosmic.Core.EngineStuff;
using static Cosmic.Core.Input;
using static SDL2.SDL;

namespace Cosmic.Desktop;

sealed partial class DesktopCosmicPlatform : ICosmicPlatform
{
    enum ExtraSDLButtons
    {
        SDL_CONTROLLER_BUTTON_ZL = (byte)(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MAX + 1),
        SDL_CONTROLLER_BUTTON_ZR,
        SDL_CONTROLLER_BUTTON_LSTICK_UP,
        SDL_CONTROLLER_BUTTON_LSTICK_DOWN,
        SDL_CONTROLLER_BUTTON_LSTICK_LEFT,
        SDL_CONTROLLER_BUTTON_LSTICK_RIGHT,
        SDL_CONTROLLER_BUTTON_RSTICK_UP,
        SDL_CONTROLLER_BUTTON_RSTICK_DOWN,
        SDL_CONTROLLER_BUTTON_RSTICK_LEFT,
        SDL_CONTROLLER_BUTTON_RSTICK_RIGHT,
        SDL_CONTROLLER_BUTTON_MAX_EXTRA,
    }

    static float LSTICK_DEADZONE = 0.3f;
    static float RSTICK_DEADZONE = 0.3f;
    static float LTRIGGER_DEADZONE = 0.3f;
    static float RTRIGGER_DEADZONE = 0.3f;

    readonly List<nint> controllers = new();

    int mouseHideTimer = 0;
    int lastMouseX = 0;
    int lastMouseY = 0;

    readonly int[] defaultKeyMappings = new int[]
    {
        (int)SDL_Scancode.SDL_SCANCODE_UP,
        (int)SDL_Scancode.SDL_SCANCODE_DOWN,
        (int)SDL_Scancode.SDL_SCANCODE_LEFT,
        (int)SDL_Scancode.SDL_SCANCODE_RIGHT,
        (int)SDL_Scancode.SDL_SCANCODE_Z,
        (int)SDL_Scancode.SDL_SCANCODE_X,
        (int)SDL_Scancode.SDL_SCANCODE_C,
        (int)SDL_Scancode.SDL_SCANCODE_RETURN
    };

    public int[] GetDefaultKeyboardMappings() => defaultKeyMappings;

    readonly int[] defaultContMappings = new int[]
    {
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP,
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN,
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT,
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT,
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A,
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B,
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X,
        (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START
    };

    public int[] GetDefaultControllerMappings() => defaultContMappings;

    public void ControllerInit(byte controllerID)
    {
        var controller = SDL_GameControllerOpen(controllerID);
        if (controller != 0)
        {
            controllers.Add(controller);
            inputType = 1;
        }
    }

    public void ControllerClose(byte controllerID)
    {
        var controller = SDL_GameControllerFromInstanceID(controllerID);
        if (controller != 0)
        {
            SDL_GameControllerClose(controller);
            controllers.Remove(controller);
        }

        if (controllers.Count == 0)
        {
            inputType = 0;
        }
    }

    public void ProcessInput()
    {
        unsafe
        {
            var keyState = (byte*)SDL_GetKeyboardState(out int length);

            if (inputType == 0)
            {
                for (int i = 0; i < (int)InputButtons.INPUT_ANY; i++)
                {
                    if (keyState[(int)inputDevice[i].keyMappings] != 0)
                    {
                        inputDevice[i].SetHeld();
                        if (!inputDevice[(int)InputButtons.INPUT_ANY].hold)
                            inputDevice[(int)InputButtons.INPUT_ANY].SetHeld();
                    }
                    else if (inputDevice[i].hold)
                        inputDevice[i].SetReleased();
                }
            }
            else if (inputType == 1)
            {
                for (int i = 0; i < (int)InputButtons.INPUT_ANY; i++)
                {
                    if (getControllerButton((byte)inputDevice[i].contMappings))
                    {
                        inputDevice[i].SetHeld();
                        if (!inputDevice[(int)InputButtons.INPUT_ANY].hold)
                            inputDevice[(int)InputButtons.INPUT_ANY].SetHeld();
                    }
                    else if (inputDevice[i].hold)
                        inputDevice[i].SetReleased();
                }
            }

            bool isPressed = false;
            for (int i = 0; i < (int)InputButtons.INPUT_BUTTONCOUNT; i++)
            {
                if (keyState[(int)inputDevice[i].keyMappings] != 0)
                {
                    isPressed = true;
                    break;
                }
            }
            if (isPressed)
                inputType = 0;
            else if (inputType == 0)
                inputDevice[(int)InputButtons.INPUT_ANY].SetReleased();

            isPressed = false;
            for (int i = 0; i < (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MAX; i++)
            {
                if (getControllerButton((byte)i))
                {
                    isPressed = true;
                    break;
                }
            }
            if (isPressed)
                inputType = 1;
            else if (inputType == 1)
                inputDevice[(int)InputButtons.INPUT_ANY].SetReleased();

            if (inputDevice[(int)InputButtons.INPUT_ANY].press || inputDevice[(int)InputButtons.INPUT_ANY].hold || touches > 1)
            {
                Engine.dimTimer = 0;
            }
            else if (Engine.dimTimer < dimLimit && !enginePaused)
            {
                ++Engine.dimTimer;
            }

            if (touches <= 0)
            {
                SDL_GetMouseState(out int mx, out int my);

                if (mx == lastMouseX && my == lastMouseY)
                {
                    ++mouseHideTimer;
                    if (mouseHideTimer == 120)
                    {
                        SDL_ShowCursor(0);
                    }
                }
                else
                {
                    if (mouseHideTimer >= 120)
                        SDL_ShowCursor(1);
                    mouseHideTimer = 0;
                    Engine.dimTimer = 0;
                }

                lastMouseX = mx;
                lastMouseY = my;
            }
        }
    }

    static float normalize(int val, int minVal, int maxVal) => ((float)(val) - (float)(minVal)) / ((float)(maxVal) - (float)(minVal));

    bool getControllerButton(byte buttonID)
    {
        bool pressed = false;

        for (int i = 0; i < controllers.Count; ++i)
        {
            var controller = controllers[i];

            if (SDL_GameControllerGetButton(controller, (SDL_GameControllerButton)buttonID) != 0)
            {
                pressed |= true;
                continue;
            }
            else
            {
                switch ((SDL_GameControllerButton)buttonID)
                {
                    default: break;
                    case SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP:
                        {
                            int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
                            float delta = 0;
                            if (axis < 0)
                                delta = -normalize(-axis, 1, 32768);
                            else
                                delta = normalize(axis, 0, 32767);
                            pressed |= delta < -LSTICK_DEADZONE;
                            continue;
                        }
                    case SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN:
                        {
                            int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
                            float delta = 0;
                            if (axis < 0)
                                delta = -normalize(-axis, 1, 32768);
                            else
                                delta = normalize(axis, 0, 32767);
                            pressed |= delta > LSTICK_DEADZONE;
                            continue;
                        }
                    case SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT:
                        {
                            int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
                            float delta = 0;
                            if (axis < 0)
                                delta = -normalize(-axis, 1, 32768);
                            else
                                delta = normalize(axis, 0, 32767);
                            pressed |= delta < -LSTICK_DEADZONE;
                            continue;
                        }
                    case SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT:
                        {
                            int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
                            float delta = 0;
                            if (axis < 0)
                                delta = -normalize(-axis, 1, 32768);
                            else
                                delta = normalize(axis, 0, 32767);
                            pressed |= delta > LSTICK_DEADZONE;
                            continue;
                        }
                }
            }

            switch ((ExtraSDLButtons)buttonID)
            {
                default: break;
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_ZL:
                    {
                        float delta = normalize(SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT), 0, 32767);
                        pressed |= delta > LTRIGGER_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_ZR:
                    {
                        float delta = normalize(SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT), 0, 32767);
                        pressed |= delta > RTRIGGER_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_LSTICK_UP:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta < -LSTICK_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_LSTICK_DOWN:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta > LSTICK_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_LSTICK_LEFT:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta > LSTICK_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_LSTICK_RIGHT:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta < -LSTICK_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_RSTICK_UP:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta < -RSTICK_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_RSTICK_DOWN:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta > RSTICK_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_RSTICK_LEFT:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta > RSTICK_DEADZONE;
                        continue;
                    }
                case ExtraSDLButtons.SDL_CONTROLLER_BUTTON_RSTICK_RIGHT:
                    {
                        int axis = SDL_GameControllerGetAxis(controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
                        float delta;
                        if (axis < 0)
                            delta = -normalize(-axis, 1, 32768);
                        else
                            delta = normalize(axis, 0, 32767);
                        pressed |= delta < -RSTICK_DEADZONE;
                        continue;
                    }
            }
        }

        return pressed;
    }
}