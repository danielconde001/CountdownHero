using System.Collections.Generic;

public static class InputControlPaths
{
    public static readonly List<string> Keyboard = new()
    {
        // Letters
        "<Keyboard>/a",
        "<Keyboard>/b",
        "<Keyboard>/c",
        "<Keyboard>/d",
        "<Keyboard>/e",
        "<Keyboard>/f",
        "<Keyboard>/g",
        "<Keyboard>/h",
        "<Keyboard>/i",
        "<Keyboard>/j",
        "<Keyboard>/k",
        "<Keyboard>/l",
        "<Keyboard>/m",
        "<Keyboard>/n",
        "<Keyboard>/o",
        "<Keyboard>/p",
        "<Keyboard>/q",
        "<Keyboard>/r",
        "<Keyboard>/s",
        "<Keyboard>/t",
        "<Keyboard>/u",
        "<Keyboard>/v",
        "<Keyboard>/w",
        "<Keyboard>/x",
        "<Keyboard>/y",
        "<Keyboard>/z",

        // Numbers
        "<Keyboard>/0",
        "<Keyboard>/1",
        "<Keyboard>/2",
        "<Keyboard>/3",
        "<Keyboard>/4",
        "<Keyboard>/5",
        "<Keyboard>/6",
        "<Keyboard>/7",
        "<Keyboard>/8",
        "<Keyboard>/9",

        // Numpad
        "<Keyboard>/numpad0",
        "<Keyboard>/numpad1",
        "<Keyboard>/numpad2",
        "<Keyboard>/numpad3",
        "<Keyboard>/numpad4",
        "<Keyboard>/numpad5",
        "<Keyboard>/numpad6",
        "<Keyboard>/numpad7",
        "<Keyboard>/numpad8",
        "<Keyboard>/numpad9",

        "<Keyboard>/numpadPlus",
        "<Keyboard>/numpadMinus",
        "<Keyboard>/numpadMultiply",
        "<Keyboard>/numpadDivide",
        "<Keyboard>/numpadEnter",

        // Function Keys
        "<Keyboard>/f1",
        "<Keyboard>/f2",
        "<Keyboard>/f3",
        "<Keyboard>/f4",
        "<Keyboard>/f5",
        "<Keyboard>/f6",
        "<Keyboard>/f7",
        "<Keyboard>/f8",
        "<Keyboard>/f9",
        "<Keyboard>/f10",
        "<Keyboard>/f11",
        "<Keyboard>/f12",
        "<Keyboard>/f13",
        "<Keyboard>/f14",
        "<Keyboard>/f15",
        "<Keyboard>/f16",
        "<Keyboard>/f17",
        "<Keyboard>/f18",
        "<Keyboard>/f19",
        "<Keyboard>/f20",
        "<Keyboard>/f21",
        "<Keyboard>/f22",

        // Modifiers
        "<Keyboard>/leftShift",
        "<Keyboard>/rightShift",

        "<Keyboard>/leftCtrl",
        "<Keyboard>/rightCtrl",

        "<Keyboard>/leftAlt",
        "<Keyboard>/rightAlt",

        "<Keyboard>/leftMeta",
        "<Keyboard>/rightMeta",

        // Navigation
        "<Keyboard>/space",
        "<Keyboard>/enter",
        "<Keyboard>/escape",
        "<Keyboard>/tab",
        "<Keyboard>/backspace",

        "<Keyboard>/insert",
        "<Keyboard>/delete",

        "<Keyboard>/home",
        "<Keyboard>/end",

        "<Keyboard>/pageUp",
        "<Keyboard>/pageDown",

        // Arrows
        "<Keyboard>/upArrow",
        "<Keyboard>/downArrow",
        "<Keyboard>/leftArrow",
        "<Keyboard>/rightArrow",

        // Symbols
        "<Keyboard>/minus",
        "<Keyboard>/equals",

        "<Keyboard>/leftBracket",
        "<Keyboard>/rightBracket",

        "<Keyboard>/backslash",

        "<Keyboard>/semicolon",
        "<Keyboard>/quote",

        "<Keyboard>/comma",
        "<Keyboard>/period",
        "<Keyboard>/slash",

        "<Keyboard>/backquote"
    };


    public static readonly List<string> Mouse = new()
    {
        "<Mouse>/leftButton",
        "<Mouse>/rightButton",
        "<Mouse>/middleButton",

        "<Mouse>/forwardButton",
        "<Mouse>/backButton",

        "<Mouse>/scroll/x",
        "<Mouse>/scroll/y",

        "<Mouse>/position/x",
        "<Mouse>/position/y"
    };


    public static readonly List<string> Gamepad = new()
    {
        // Face Buttons
        "<Gamepad>/buttonSouth",
        "<Gamepad>/buttonEast",
        "<Gamepad>/buttonWest",
        "<Gamepad>/buttonNorth",

        // Shoulders
        "<Gamepad>/leftShoulder",
        "<Gamepad>/rightShoulder",

        // Triggers
        "<Gamepad>/leftTrigger",
        "<Gamepad>/rightTrigger",        
        
        // Stick Movement
        "<Gamepad>/leftStick",
        "<Gamepad>/leftStick/x",
        "<Gamepad>/leftStick/y",
        "<Gamepad>/leftStickPress",

        "<Gamepad>/rightStick",
        "<Gamepad>/rightStick/x",
        "<Gamepad>/rightStick/y",
        "<Gamepad>/rightStickPress",

        // DPad
        "<Gamepad>/dpad",
        "<Gamepad>/dpad/up",
        "<Gamepad>/dpad/down",
        "<Gamepad>/dpad/left",
        "<Gamepad>/dpad/right",

        // Menu Buttons
        "<Gamepad>/start",
        "<Gamepad>/select",

        // Xbox Guide / PlayStation PS Button
        "<Gamepad>/buttonGuide",

        // Touchpad
        "<Gamepad>/touchpad",
        "<Gamepad>/touchpad/press",

        "<Gamepad>/touchpad/x",
        "<Gamepad>/touchpad/y",
    };
}