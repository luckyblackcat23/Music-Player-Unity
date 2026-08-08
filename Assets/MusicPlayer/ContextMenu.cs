using UnityEngine.UIElements;
using UnityEngine;
using System;
using System.Collections.Generic;

public class ContextMenu
{
    readonly VisualElement root;
    readonly VisualElement menu;

    public bool IsOpen => menu.style.display == DisplayStyle.Flex;

    List<ContextMenuItem> items = new();

    public ContextMenu(VisualElement root)
    {
        this.root = root;

        menu = new VisualElement();
        menu.name = "ContextMenu";
        menu.AddToClassList("context-menu");

        menu.style.display = DisplayStyle.None;

        root.Add(menu);

        // Prevent clicking the menu from closing itself.
        menu.RegisterCallback<PointerDownEvent>(e =>
        {
            e.StopPropagation();
        });

        // Clicking anywhere else closes it.
        root.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!menu.worldBound.Contains(e.position))
            {
                Hide();
            }
        });

        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
                Hide();
        });
    }

    public void AddItem(string label, Action action)
    {
        items.Add(new ContextMenuItem(label, action));
    }

    public void Show(Vector2 position)
    {
        menu.Clear();

        foreach (ContextMenuItem item in items)
        {
            Button button = new Button();

            button.text = item.Text;

            button.SetEnabled(item.Enabled);

            button.clicked += () =>
            {
                Hide();
                item.Action?.Invoke();
            };

            menu.Add(button);
        }

        menu.style.left = position.x;
        menu.style.top = position.y;

        menu.style.display = DisplayStyle.Flex;
        menu.BringToFront();

        menu.schedule.Execute(() =>
        {
            float x = Mathf.Min(position.x, root.resolvedStyle.width - menu.resolvedStyle.width);
            float y = Mathf.Min(position.y, root.resolvedStyle.height - menu.resolvedStyle.height);

            menu.style.left = Mathf.Max(0, x);
            menu.style.top = Mathf.Max(0, y);
        });

        items.Clear();
    }

    public void Hide()
    {
        menu.style.display = DisplayStyle.None;
    }
}

public class ContextMenuItem
{
    public string Text { get; }
    public Action Action { get; }
    public bool Enabled { get; }

    public bool isSeperator { get; }

    public ContextMenuItem(string text, Action action, bool enabled = true)
    {
        Text = text;
        Action = action;
        Enabled = enabled;

    }

    /* 
    finish implementing later
 
    public static ContextMenuItem Separator()
    {
        return new ContextMenuItem
        {
            isSeperator = true
        };
    }
    */
}