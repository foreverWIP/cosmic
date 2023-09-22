global using static Cosmic.Core.PlayerStuff;
using Cosmic.Formats;

namespace Cosmic.Core;

static class PlayerStuff
{
    public const int PLAYER_COUNT = (2);

    public enum PlayerControlModes
    {
        CONTROLMODE_NONE = -1,
        CONTROLMODE_NORMAL = 0,
        CONTROLMODE_SIDEKICK = 1,
    }

    public struct Player
    {
        public int entityNo;
        public int XPos;
        public int YPos;
        public int XVelocity;
        public int YVelocity;
        public int speed;
        public int screenXPos;
        public int screenYPos;
        public int angle;
        public int timer;
        public int lookPos;
        public int[] values
        {
            get
            {
                return _values ??= new int[8];
            }
        }
        int[] _values;
        public byte collisionMode;
        public byte skidding;
        public byte pushing;
        public byte collisionPlane;
        public sbyte controlMode;
        public byte controlLock;
        public int topSpeed;
        public int acceleration;
        public int deceleration;
        public int airAcceleration;
        public int airDeceleration;
        public int gravityStrength;
        public int jumpStrength;
        public int jumpCap;
        public int rollingAcceleration;
        public int rollingDeceleration;
        public byte visible;
        public byte tileCollisions;
        public byte objectInteractions;
        public byte left;
        public byte right;
        public byte up;
        public byte down;
        public byte jumpPress;
        public byte jumpHold;
        public byte followPlayer1;
        public byte trackScroll;
        public byte gravity;
        public byte water;
        public byte[] flailing
        {
            get
            {
                return _flailing ??= new byte[3];
            }
        }
        byte[] _flailing;
        public Animation.AnimationFile? animationFile;
        public int boundEntity;
    }

    public static readonly Player[] playerList = new Player[PLAYER_COUNT];
    public static readonly string[] playerNames = new string[PLAYER_COUNT];

    public static int playerListPos = 0;
    public static int activePlayer = 0;
    public static int activePlayerCount = 1;

    static ushort upBuffer = 0;
    static ushort downBuffer = 0;
    static ushort leftBuffer = 0;
    static ushort rightBuffer = 0;
    static ushort jumpPressBuffer = 0;
    static ushort jumpHoldBuffer = 0;

    public static void ProcessPlayerControl(ref Player player)
    {
        switch ((PlayerControlModes)player.controlMode)
        {
            case PlayerControlModes.CONTROLMODE_NORMAL:
            default:
                player.up = (byte)(keyDown[0].up ? 1 : 0);
                player.down = (byte)(keyDown[0].down ? 1 : 0);
                if (!keyDown[0].left || !keyDown[0].right)
                {
                    player.left = (byte)(keyDown[0].left ? 1 : 0);
                    player.right = (byte)(keyDown[0].right ? 1 : 0);
                }
                else
                {
                    player.left = 0;
                    player.right = 0;
                }
                player.jumpHold = (byte)((keyDown[0].C || keyDown[0].B || keyDown[0].A) ? 1 : 0);
                player.jumpPress = (byte)((keyPress[0].C || keyPress[0].B || keyPress[0].A) ? 1 : 0);
                upBuffer <<= 1;
                upBuffer |= (byte)player.up;
                downBuffer <<= 1;
                downBuffer |= (byte)player.down;
                leftBuffer <<= 1;
                leftBuffer |= (byte)player.left;
                rightBuffer <<= 1;
                rightBuffer |= (byte)player.right;
                jumpPressBuffer <<= 1;
                jumpPressBuffer |= (byte)player.jumpPress;
                jumpHoldBuffer <<= 1;
                jumpHoldBuffer |= (byte)player.jumpHold;
                break;

            case PlayerControlModes.CONTROLMODE_NONE:
                upBuffer <<= 1;
                upBuffer |= (byte)player.up;
                downBuffer <<= 1;
                downBuffer |= (byte)player.down;
                leftBuffer <<= 1;
                leftBuffer |= (byte)player.left;
                rightBuffer <<= 1;
                rightBuffer |= (byte)player.right;
                jumpPressBuffer <<= 1;
                jumpPressBuffer |= (byte)player.jumpPress;
                jumpHoldBuffer <<= 1;
                jumpHoldBuffer |= (byte)player.jumpHold;
                break;

            case PlayerControlModes.CONTROLMODE_SIDEKICK:
                player.up = (byte)(((upBuffer >> 15) != 0) ? 1 : 0);
                player.down = (byte)(((downBuffer >> 15) != 0) ? 1 : 0);
                player.left = (byte)(((leftBuffer >> 15) != 0) ? 1 : 0);
                player.right = (byte)(((rightBuffer >> 15) != 0) ? 1 : 0);
                player.jumpPress = (byte)(((jumpPressBuffer >> 15) != 0) ? 1 : 0);
                player.jumpHold = (byte)(((jumpHoldBuffer >> 15) != 0) ? 1 : 0);
                break;
        }
    }
}