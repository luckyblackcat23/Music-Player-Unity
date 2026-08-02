using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ContextMenu
{
    readonly VisualElement root;
    readonly VisualElement menu;

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
            Hide();
        }, TrickleDown.TrickleDown);
    }

    public void Show(Vector2 position, params ContextMenuItem[] items)
    {
        menu.Clear();

        foreach (var item in items)
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
    }

    public void Hide()
    {
        menu.style.display = DisplayStyle.None;
    }
}

public class ContextMenuItem
{
    public string Text;
    public System.Action Action;
    public bool Enabled = true;

    public ContextMenuItem(string text, System.Action action, bool enabled = true)
    {
        Text = text;
        Action = action;
        Enabled = enabled;
    }
}