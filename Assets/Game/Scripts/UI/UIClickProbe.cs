using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Debug tool. On every left click, logs every UI object under the mouse,
/// topmost first. The first line is the object that receives the click; if a
/// button isn't responding, whatever is listed above it is in the way.
///
/// Setup: add to the EventSystem object, press Play, click. Delete once done.
/// </summary>
public class UIClickProbe : MonoBehaviour
{
    private readonly List<RaycastResult> m_hits = new();

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || EventSystem.current == null) return;

        PointerEventData pointer = new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() };
        EventSystem.current.RaycastAll(pointer, m_hits);

        StringBuilder report = new StringBuilder($"[UIClickProbe] {m_hits.Count} UI object(s) under the mouse, topmost first:");
        foreach (RaycastResult hit in m_hits)
        {
            GameObject clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            report.Append($"\n  {Path(hit.gameObject.transform)}  →  click goes to: {(clickTarget != null ? clickTarget.name : "nothing")}");
        }

        Debug.Log(report.ToString());
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        for (t = t.parent; t != null; t = t.parent) path = $"{t.name}/{path}";
        return path;
    }
}
