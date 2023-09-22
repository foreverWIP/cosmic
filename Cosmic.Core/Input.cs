global using static Cosmic.Core.Input;

namespace Cosmic.Core;

public static class Input
{
    const int NUM_POSSIBLE_PLAYERS = 4;

    public enum InputButtons
    {
        INPUT_UP,
        INPUT_DOWN,
        INPUT_LEFT,
        INPUT_RIGHT,
        INPUT_BUTTONA,
        INPUT_BUTTONB,
        INPUT_BUTTONC,
        INPUT_START,
        INPUT_ANY,
        INPUT_BUTTONCOUNT,
    }

    public struct InputData
    {
        public bool up;
        public bool down;
        public bool left;
        public bool right;
        public bool A;
        public bool B;
        public bool C;
        public bool start;
    }

    public struct InputButton
    {
        public bool press, hold;
        public int keyMappings;
        public int contMappings;

        public void SetHeld()
        {
            press = !hold;
            hold = true;
        }
        public void SetReleased()
        {
            press = false;
            hold = false;
        }

        public readonly bool Down() { return press || hold; }
    }

    public enum DefaultHapticIDs
    {
        HAPTIC_NONE = -2,
        HAPTIC_STOP = -1,
    }

    public static int GetHapticEffectNum()
    {
        int num = hapticEffectNum;
        hapticEffectNum = (int)DefaultHapticIDs.HAPTIC_NONE;
        return num;
    }

    public static readonly InputData[] keyPress = new InputData[NUM_POSSIBLE_PLAYERS];
    public static readonly InputData[] keyDown = new InputData[NUM_POSSIBLE_PLAYERS];

    public static readonly bool[] anyPress = new bool[NUM_POSSIBLE_PLAYERS];

    public static readonly int[] touchDown = new int[8];
    public static readonly int[] touchX = new int[8];
    public static readonly int[] touchY = new int[8];
    public static int touches = 0;

    static int hapticEffectNum = -2;

    enum AndroidHapticIDs
    {
        HAPTIC_SHARP_CLICK_100 = 0,
        HAPTIC_SHARP_CLICK_66 = 1,
        HAPTIC_SHARP_CLICK_33 = 2,
        HAPTIC_STRONG_CLICK_100 = 3,
        HAPTIC_STRONG_CLICK_66 = 4,
        HAPTIC_STRONG_CLICK_33 = 5,
        HAPTIC_BUMP_100 = 6,
        HAPTIC_BUMP_66 = 7,
        HAPTIC_BUMP_33 = 8,
        HAPTIC_BOUNCE_100 = 9,
        HAPTIC_BOUNCE_66 = 10,
        HAPTIC_BOUNCE_33 = 11,
        HAPTIC_DOUBLE_SHARP_CLICK_100 = 12,
        HAPTIC_DOUBLE_SHARP_CLICK_66 = 13,
        HAPTIC_DOUBLE_SHARP_CLICK_33 = 14,
        HAPTIC_DOUBLE_STRONG_CLICK_100 = 15,
        HAPTIC_DOUBLE_STRONG_CLICK_66 = 16,
        HAPTIC_DOUBLE_STRONG_CLICK_33 = 17,
        HAPTIC_DOUBLE_BUMP_100 = 18,
        HAPTIC_DOUBLE_BUMP_66 = 19,
        HAPTIC_DOUBLE_BUMP_33 = 20,
        HAPTIC_TRIPLE_STRONG_CLICK_100 = 21,
        HAPTIC_TRIPLE_STRONG_CLICK_66 = 22,
        HAPTIC_TRIPLE_STRONG_CLICK_33 = 23,
        HAPTIC_TICK_100 = 24,
        HAPTIC_TICK_66 = 25,
        HAPTIC_TICK_33 = 26,
        HAPTIC_LONG_BUZZ_100 = 27,
        HAPTIC_LONG_BUZZ_66 = 28,
        HAPTIC_LONG_BUZZ_33 = 29,
        HAPTIC_SHORT_BUZZ_100 = 30,
        HAPTIC_SHORT_BUZZ_66 = 31,
        HAPTIC_SHORT_BUZZ_33 = 32,
        HAPTIC_LONG_TRANSITION_RAMP_UP_100 = 33,
        HAPTIC_LONG_TRANSITION_RAMP_UP_66 = 34,
        HAPTIC_LONG_TRANSITION_RAMP_UP_33 = 35,
        HAPTIC_SHORT_TRANSITION_RAMP_UP_100 = 36,
        HAPTIC_SHORT_TRANSITION_RAMP_UP_66 = 37,
        HAPTIC_SHORT_TRANSITION_RAMP_UP_33 = 38,
        HAPTIC_LONG_TRANSITION_RAMP_DOWN_100 = 39,
        HAPTIC_LONG_TRANSITION_RAMP_DOWN_66 = 40,
        HAPTIC_LONG_TRANSITION_RAMP_DOWN_33 = 41,
        HAPTIC_SHORT_TRANSITION_RAMP_DOWN_100 = 42,
        HAPTIC_SHORT_TRANSITION_RAMP_DOWN_66 = 43,
        HAPTIC_SHORT_TRANSITION_RAMP_DOWN_33 = 44,
        HAPTIC_FAST_PULSE_100 = 45,
        HAPTIC_FAST_PULSE_66 = 46,
        HAPTIC_FAST_PULSE_33 = 47,
        HAPTIC_FAST_PULSING_100 = 48,
        HAPTIC_FAST_PULSING_66 = 49,
        HAPTIC_FAST_PULSING_33 = 50,
        HAPTIC_SLOW_PULSE_100 = 51,
        HAPTIC_SLOW_PULSE_66 = 52,
        HAPTIC_SLOW_PULSE_33 = 53,
        HAPTIC_SLOW_PULSING_100 = 54,
        HAPTIC_SLOW_PULSING_66 = 55,
        HAPTIC_SLOW_PULSING_33 = 56,
        HAPTIC_TRANSITION_BUMP_100 = 57,
        HAPTIC_TRANSITION_BUMP_66 = 58,
        HAPTIC_TRANSITION_BUMP_33 = 59,
        HAPTIC_TRANSITION_BOUNCE_100 = 60,
        HAPTIC_TRANSITION_BOUNCE_66 = 61,
        HAPTIC_TRANSITION_BOUNCE_33 = 62,
        HAPTIC_ALERT1 = 63,
        HAPTIC_ALERT2 = 64,
        HAPTIC_ALERT3 = 65,
        HAPTIC_ALERT4 = 66,
        HAPTIC_ALERT5 = 67,
        HAPTIC_ALERT6 = 68,
        HAPTIC_ALERT7 = 69,
        HAPTIC_ALERT8 = 70,
        HAPTIC_ALERT9 = 71,
        HAPTIC_ALERT10 = 72,
        HAPTIC_EXPLOSION1 = 73,
        HAPTIC_EXPLOSION2 = 74,
        HAPTIC_EXPLOSION3 = 75,
        HAPTIC_EXPLOSION4 = 76,
        HAPTIC_EXPLOSION5 = 77,
        HAPTIC_EXPLOSION6 = 78,
        HAPTIC_EXPLOSION7 = 79,
        HAPTIC_EXPLOSION8 = 80,
        HAPTIC_EXPLOSION9 = 81,
        HAPTIC_EXPLOSION10 = 82,
        HAPTIC_WEAPON1 = 83,
        HAPTIC_WEAPON2 = 84,
        HAPTIC_WEAPON3 = 85,
        HAPTIC_WEAPON4 = 86,
        HAPTIC_WEAPON5 = 87,
        HAPTIC_WEAPON6 = 88,
        HAPTIC_WEAPON7 = 89,
        HAPTIC_WEAPON8 = 90,
        HAPTIC_WEAPON9 = 91,
        HAPTIC_WEAPON10 = 92,
        HAPTIC_IMPACT_WOOD_100 = 93,
        HAPTIC_IMPACT_WOOD_66 = 94,
        HAPTIC_IMPACT_WOOD_33 = 95,
        HAPTIC_IMPACT_METAL_100 = 96,
        HAPTIC_IMPACT_METAL_66 = 97,
        HAPTIC_IMPACT_METAL_33 = 98,
        HAPTIC_IMPACT_RUBBER_100 = 99,
        HAPTIC_IMPACT_RUBBER_66 = 100,
        HAPTIC_IMPACT_RUBBER_33 = 101,
        HAPTIC_TEXTURE1 = 102,
        HAPTIC_TEXTURE2 = 103,
        HAPTIC_TEXTURE3 = 104,
        HAPTIC_TEXTURE4 = 105,
        HAPTIC_TEXTURE5 = 106,
        HAPTIC_TEXTURE6 = 107,
        HAPTIC_TEXTURE7 = 108,
        HAPTIC_TEXTURE8 = 109,
        HAPTIC_TEXTURE9 = 110,
        HAPTIC_TEXTURE10 = 111,
        HAPTIC_ENGINE1_100 = 112,
        HAPTIC_ENGINE1_66 = 113,
        HAPTIC_ENGINE1_33 = 114,
        HAPTIC_ENGINE2_100 = 115,
        HAPTIC_ENGINE2_66 = 116,
        HAPTIC_ENGINE2_33 = 117,
        HAPTIC_ENGINE3_100 = 118,
        HAPTIC_ENGINE3_66 = 119,
        HAPTIC_ENGINE3_33 = 120,
        HAPTIC_ENGINE4_100 = 121,
        HAPTIC_ENGINE4_66 = 122,
        HAPTIC_ENGINE4_33 = 123,
    }

    public static readonly InputButton[] inputDevice = new InputButton[(int)InputButtons.INPUT_BUTTONCOUNT];
    public static int inputType = 0;

    public static void CheckKeyPress(ref InputData input, byte flags)
    {
        if ((flags & 0x1) != 0)
            input.up = inputDevice[(int)InputButtons.INPUT_UP].press;
        if ((flags & 0x2) != 0)
            input.down = inputDevice[(int)InputButtons.INPUT_DOWN].press;
        if ((flags & 0x4) != 0)
            input.left = inputDevice[(int)InputButtons.INPUT_LEFT].press;
        if ((flags & 0x8) != 0)
            input.right = inputDevice[(int)InputButtons.INPUT_RIGHT].press;
        if ((flags & 0x10) != 0)
            input.A = inputDevice[(int)InputButtons.INPUT_BUTTONA].press;
        if ((flags & 0x20) != 0)
            input.B = inputDevice[(int)InputButtons.INPUT_BUTTONB].press;
        if ((flags & 0x40) != 0)
            input.C = inputDevice[(int)InputButtons.INPUT_BUTTONC].press;
        if ((flags & 0x80) != 0)
            input.start = inputDevice[(int)InputButtons.INPUT_START].press;
        if ((flags & 0x80) != 0)
        {
            anyPress[0] = inputDevice[(int)InputButtons.INPUT_ANY].press;
            if (!anyPress[0])
            {
                for (int t = 0; t < touches; ++t)
                {
                    if (touchDown[t] != 0)
                        anyPress[0] = true;
                }
            }

            SetGlobalVariableByName("input.pressButton", anyPress[0] ? 1 : 0);
        }
    }

    public static void CheckKeyDown(ref InputData input, byte flags)
    {
        if ((flags & 0x1) != 0)
            input.up = inputDevice[(int)InputButtons.INPUT_UP].hold;
        if ((flags & 0x2) != 0)
            input.down = inputDevice[(int)InputButtons.INPUT_DOWN].hold;
        if ((flags & 0x4) != 0)
            input.left = inputDevice[(int)InputButtons.INPUT_LEFT].hold;
        if ((flags & 0x8) != 0)
            input.right = inputDevice[(int)InputButtons.INPUT_RIGHT].hold;
        if ((flags & 0x10) != 0)
            input.A = inputDevice[(int)InputButtons.INPUT_BUTTONA].hold;
        if ((flags & 0x20) != 0)
            input.B = inputDevice[(int)InputButtons.INPUT_BUTTONB].hold;
        if ((flags & 0x40) != 0)
            input.C = inputDevice[(int)InputButtons.INPUT_BUTTONC].hold;
        if ((flags & 0x80) != 0)
            input.start = inputDevice[(int)InputButtons.INPUT_START].hold;
    }

    public static void QueueHapticEffect(int hapticID)
    {
        if (Engine.hapticsEnabled)
        {
            // Haptic ID seems to be the ID for "Universal Haptic Layer"'s haptic effect library
            hapticEffectNum = hapticID;
        }
    }

    public static void PlayHaptics(int left, int right, int power) { }
}