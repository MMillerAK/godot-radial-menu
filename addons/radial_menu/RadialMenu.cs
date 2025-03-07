using Godot;
using System;

[Tool]
[GlobalClass]
public partial class RadialMenu : Container
{
    [Signal]
    public delegate void HoveredEventHandler(Node child);
    [Signal]
    public delegate void SelectedEventHandler(Node child);

    const float MINWIDTH = 0.01f;
    private PackedScene centerNode = null;
    private CenterContainer center;
    private float maxWidth = 1.0f;
    private float minWidth = 0.5f;
    private float cursorSize = 0.4f;
    private float cursorDegrees = 0.4f;
    private float cursorTarget = 0.4f;
    private Color bgColor = new Color("202431");
    private Color fgColor = new Color("595f70");
    private float bevelWidth = 0.5f;
    private bool bevelEnabled = false;
    private Color bevelColor = new Color("333a4f");
    private bool modulateEnabled = false;
    private Color modulateHover = Colors.White;
    private Color modulateDefault = new Color("b6b6b6");
    private Tween snapTween;

    [Export]
    public PackedScene CenterNode
    {
        get => centerNode;
        set
        {
            centerNode = value;
            var menu = GetNode("RadialMenu/CenterNode");
            var oldNodes = menu.GetChildren();
            foreach (Node child in oldNodes)
            {
                child.Visible = false;
                child.QueueFree();
            }
            if (value == null)
            {
                return;
            }
            menu.AddChild(value.Instantiate());
        }
    }

    [Export]
    public bool Snap { get; set; }

    [Export(PropertyHint.Range, "0,3.14, 0.1")]
    public float CursorSize
    {
        get => cursorSize;
        set
        {
            cursorSize = value;
            SetShaderParameter("cursor_size", value);
        }
    }

    [Export(PropertyHint.Range, "-3.14,3.14, 0.1")]
    public float CursorDegrees
    {
        get => cursorDegrees;
        set
        {
            cursorDegrees = value;
            SetShaderParameter("cursor_deg", cursorDegrees);
        }
    }

    [Export]
    public float CursorTarget
    {
        get => cursorTarget;
        set
        {
            cursorTarget = value;
            if (Snap && IsInsideTree())
            {
                float indexOffset = (float)CursorPosition.Call("get_index_offset");
                CursorDegrees = Godot.Mathf.Snapped(cursorTarget, indexOffset);
            }
            else
            {
                CursorDegrees = value;
            }
        }
    }

    [Export]
    public Color BgColor
    {
        get => bgColor;
        set
        {
            bgColor = value;
            SetShaderParameter("color_bg", value);
        }
    }

    [Export]
    public Color FgColor
    {
        get => fgColor;
        set
        {
            fgColor = value;
            SetShaderParameter("color_fg", value);
        }
    }

    [Export(PropertyHint.Range, "0,1")]
    public float BevelWidth
    {
        get => bevelWidth;
        set
        {
            bevelWidth = value;
            SetShaderParameter("bevel_width", value / 5.0f);
        }
    }

    [Export]
    public bool BevelEnabled
    {
        get => bevelEnabled;
        set
        {
            bevelEnabled = value;
            SetShaderParameter("bevel_enabled", value);
        }
    }

    [Export]
    public Color BevelColor
    {
        get => bevelColor;
        set
        {
            bevelColor = value;
            SetShaderParameter("bevel_color", value);
        }
    }

    [Export]
    public bool ModulateEnabled
    {
        get => modulateEnabled;
        set
        {
            modulateEnabled = value;
            DoModulate();
        }
    }

    [Export]
    public Color ModulateHover
    {
        get => modulateHover;
        set
        {
            modulateHover = value;
            DoModulate();
        }
    }

    [Export]
    public Color ModulateDefault
    {
        get => modulateDefault;
        set
        {
            modulateDefault = value;
            DoModulate();
        }
    }

    [Export(PropertyHint.Range, "0,1")]
    public float MaxWidth
    {
        get => maxWidth;
        set
        {
            if (value - MINWIDTH < 0)
                return;

            maxWidth = value;
            SetShaderParameter("width_max", value);

            // Handle case where we're now smaller than the minimum size
            if (value - minWidth < MINWIDTH)
                SetMinWidth(value - MINWIDTH * 2);

            EmitSignal(SignalName.SortChildren);
        }
    }

    [Export(PropertyHint.Range, "0,1")]
    public float MinWidth
    {
        get => minWidth;
        set
        {
            if (value + MINWIDTH > 1)
                return;

            minWidth = value;
            SetShaderParameter("width_min", value);

            // Handle case where we're now bigger than the minimum size
            if (maxWidth - value < MINWIDTH)
                SetMaxWidth(value + MINWIDTH * 2);

            var minSize = MinimumSize * value;
            if (center != null)
                center.CustomMinimumSize = new Vector2(minSize, minSize);

            EmitSignal(SignalName.SortChildren);
        }
    }

    public float MinimumSize
    {
        get
        {
            Vector2 size = Size;
            return Mathf.Min(size.X, size.Y);
        }
    }

    public Control CursorPosition
    {
        get
        {
            return GetNode<Control>("RadialMenu/CursorPos");
        }
    }

    public void SetShaderParameter(string name, Variant value)
    {
        if (GetChildren().Count == 0)
            return;

        var background = GetNode<ColorRect>("RadialMenu/Background");
        if (background != null && background.Material is ShaderMaterial material)
        {
            material.SetShaderParameter(name, value);
        }
    }

    public void SetMaxWidth(float value)
    {
        MaxWidth = value;
    }

    public void SetMinWidth(float value)
    {
        MinWidth = value;
    }

    public void AddButton(Control button)
    {
        AddChild(button);
        PlaceButtons();
    }

    private void Setup()
    {
        // Initialize all parameters like in GDScript version
        Snap = Snap;
        BevelColor = bevelColor;
        BevelEnabled = bevelEnabled;
        BevelWidth = bevelWidth;
        CenterNode = centerNode;
        BgColor = bgColor;
        FgColor = fgColor;
        CursorDegrees = cursorDegrees;
        CursorTarget = cursorTarget;
        CursorSize = cursorSize;
        ModulateDefault = modulateDefault;
        ModulateEnabled = modulateEnabled;
        ModulateHover = modulateHover;
        MaxWidth = maxWidth;
        MinWidth = minWidth;
    }

    private Godot.Collections.Array<Node> GetChildren()
    {
        Godot.Collections.Array<Node> results = ((Node)this).GetChildren();
        if (results.Count > 0)
            results.RemoveAt(0);
        return results;
    }

    private void PlaceButtons()
    {
        var buttons = GetChildren();
        if (buttons.Count == 0)
            return;

        float angleIncrement = (2 * Mathf.Pi) / buttons.Count;
        Rect2 rect = GetRect();
        Vector2 center = rect.Size / 2;
        float minSizeF = MinimumSize;
        Vector2 minSize = new Vector2(minSizeF, minSizeF);
        float angle = 0;

        foreach (Control button in buttons)
        {
            Vector2 pos = Vector2.FromAngle(angle);
            pos = pos * minSize / 2;

            if (minSize.LengthSquared() > 0)
            {
                pos *= Vector2.One - (button.Size / minSize) * 3;
                pos = pos - button.Size / 2;
                pos = pos + center;
                button.Position = pos;
                angle += angleIncrement;
                button.FocusMode = Control.FocusModeEnum.None;
            }
        }

        DoModulate();
    }

    private void OnSortChildren()
    {
        PlaceButtons();
        float minSizeF = MinimumSize;

        var radial = GetNode<Control>("RadialMenu");
        radial.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var background = GetNode<Control>("RadialMenu/Background");
        background.CustomMinimumSize = new Vector2(minSizeF, minSizeF);
        background.PivotOffset = new Vector2(minSizeF / 2, minSizeF / 2);

        if (!Engine.IsEditorHint())
        {
            CursorPosition.Call("set_count", GetChildren().Count);
        }
    }

    private void OnSelected(int index)
    {
        var children = GetChildren();
        if (index < 0 || index >= children.Count)
            return;

        Node child = children[index];
        if (child is BaseButton button)
        {
            button.ButtonPressed = true;
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        EmitSignal(SignalName.Selected, child);

        if (modulateEnabled)
            DoModulate();
    }

    private void OnHover(int index)
    {
        var children = GetChildren();
        if (index < 0 || index >= children.Count)
            return;

        Node child = children[index];
        EmitSignal(SignalName.Hovered, child);

        if (modulateEnabled)
            DoModulate(child as CanvasItem);
    }

    private void DoModulate(CanvasItem hovered = null)
    {
        Color defaultColor = Colors.White;

        if (modulateEnabled)
        {
            defaultColor = modulateDefault;
        }

        foreach (CanvasItem child in GetChildren())
        {
            child.Modulate = defaultColor;
        }

        if (hovered != null)
        {
            hovered.Modulate = modulateHover;
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        if (CursorPosition != null && CursorPosition.Has("cursor"))
        {
            Vector2 pos = (Vector2)CursorPosition.Get("cursor");
            CursorTarget = Mathf.Atan2(pos.Y, pos.X);
        }
    }

    public RadialMenu()
    {
        // Use a relative path for loading the scene
        PackedScene scene = GD.Load<PackedScene>("res://addons/radial_menu/RadialMenu.tscn");
        AddChild(scene.Instantiate());
    }

    public override void _Ready()
    {
        base._Ready();

        // Make sure to get the center node
        center = GetNode<CenterContainer>("RadialMenu/CenterNode");

        // Connect signals
        CursorPosition.Connect("hover", Callable.From<int>(OnHover));
        CursorPosition.Connect("selected", Callable.From<int>(OnSelected));

        // Setup and place buttons
        Setup();
        PlaceButtons();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationSortChildren)
        {
            OnSortChildren();
        }
    }
}
