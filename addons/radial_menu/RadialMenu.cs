using Godot;
using System;
//Custom wrapper for the radial menu
[Tool]
[GlobalClass]
public partial class RadialMenu : Container
{
    [Signal]
    public delegate void HoveredEventHandler(Node child);
    [Signal]
    public delegate void SelectedEventHandler(Node child);
    const float MINWIDTH = 0.01f;
    [Export]
    public PackedScene CenterNode
    {
        get => centerNode;
        set
        {
            centerNode = value;
            var menu = GetNode("RadialMenu/CenterNode");
            var oldNodes = menu.GetChildren();
            foreach (Node2D child in oldNodes)
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
    public bool Snap;
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
    public float BevelWidth { get => bevelWidth; set => bevelWidth = value; }
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
    [Export] public bool ModulateEnabled { get => modulateEnabled; set => modulateEnabled = value; }
    [Export] public Color ModulateHover { get => modulateHover; set => modulateHover = value; }
    [Export] public Color ModulateDefault { get => modulateDefault; set => modulateDefault = value; }
    public float MinimumSize
    {
        get
        {
            Vector2 size = Size;
            return Mathf.Min(size.X, size.Y); ;
        }
    }
    public Control CursorPosition
    {
        get
        {
            Node node = GetNode<Control>("RadialMenu/CursorPos");
            return (Control)node;
        }
    }
    private PackedScene centerNode = null;
    private CenterContainer center = null;
    [Export(PropertyHint.Range, "0,1")]
    private float MaxWidth = 1.0f;
    [Export(PropertyHint.Range, "0,1")]
    private float MinWidth = 0.5f;
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
    public void SetMaxWidth(float value)
    {
        if (value - MinWidth < 0)
        {
            return;
        }
        MaxWidth = value;
        this.SetShaderParameter("width_max", value);
        if (value - MinWidth < MINWIDTH)
        {
            SetMinWidth(value);
        }
        EmitSignal(SignalName.SortChildren);
    }
    public float GetMaxWidth()
    {
        return MaxWidth;
    }
    public void SetMinWidth(float value)
    {
        {
            if (value + MinWidth > 1)
            {
                return;
            }
            MinWidth = value;
            this.SetShaderParameter("width_min", value);
            if (MaxWidth - value < MINWIDTH)
            {
                SetMaxWidth(value + MinWidth * 2);
            }
            center.CustomMinimumSize = new Vector2(MinWidth, MinWidth);
            EmitSignal(SignalName.SortChildren);
        }
    }
    public float GetMinWidth()
    {
        return MinWidth;
    }
    public void SetShaderParameter(string name, Variant value)
    {
        if (GetChildren().Count == 0)
        {
            return;
        }
        ((ShaderMaterial)GetNode<ColorRect>("RadialMenu/Background").Material).SetShaderParameter(name, value);
    }
    public void AddButton(Control button)
    {
        AddChild(button);
        PlaceButtons();
    }
    private void Setup()
    {
        OnSortChildren();
        DoModulate();
    }
    private Godot.Collections.Array<Node> GetChildren()
    {
        Godot.Collections.Array<Node> results = ((Node)this).GetChildren();
        results.RemoveAt(0);
        return results;
    }
    private void PlaceButtons()
    {
        var buttons = GetChildren();
        if (buttons.Count == 0)
        {
            return;
        }
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
                button.SetPosition(pos);
                angle += angleIncrement;
                button.FocusMode = FocusModeEnum.None;
            }
        }
        DoModulate();
    }
    private void OnSortChildren()
    {
        PlaceButtons();
        float minSizeF = MinimumSize;
        Control radial = (Control)GetNode("RadialMenu");
        radial.AnchorLeft = (float)Anchor.Begin;
        radial.AnchorTop = (float)Anchor.Begin;
        radial.AnchorRight = (float)Anchor.End;
        radial.AnchorBottom = (float)Anchor.End;
        Control background = (Control)GetNode("RadialMenu/Background");
        background.CustomMinimumSize = new Vector2(minSizeF, minSizeF);
        background.PivotOffset = new Vector2(minSizeF / 2, minSizeF / 2);
        if (!Engine.IsEditorHint())
        {
            CursorPosition.Call("set_count", GetChildren().Count);
        }
    }
    private void OnSelected(int index)
    {
        CanvasItem child = (CanvasItem)GetChild(index);
        if (child is BaseButton button)
        {
            button.ButtonPressed = true;
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        EmitSignal(SignalName.Selected, child);
        if (modulateEnabled)
            DoModulate((CanvasItem)child);
    }
    private void OnHover(int index)
    {
        var child = GetChild(index);
        EmitSignal(SignalName.Hovered, child);
        if (modulateEnabled)
            DoModulate((CanvasItem)child);
    }
    private void DoModulate(CanvasItem Hovered = null)
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
        if (Hovered != null)
        {
            Hovered.Modulate = modulateHover;
        }
    }
    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        Vector2 pos = (Vector2)CursorPosition.Get("cursor");
        CursorTarget = Mathf.Atan2(pos.Y, pos.X);
    }
    public RadialMenu()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>("./RadialMenu.tscn");
        AddChild(scene.Instantiate());
    }
    public override void _Ready()
    {
        base._Ready();
        CursorPosition.Connect("hover", Callable.From<int>(OnHover));
        CursorPosition.Connect("selected", Callable.From<int>(OnSelected));
        Setup();
    }
    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationSortChildren)
        {
            this.OnSortChildren();
        }
    }
}