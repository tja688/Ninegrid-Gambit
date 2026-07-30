using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MouseControl : MonoBehaviour
{
    public GameObject CreateFild;
    public Collider2D DamageFild;
    public Collider2D BurnFild;

    public GameObject CardBlank;

    GameObject objSelected = null;
    Vector3 OffSet = new Vector3();

    void Update()
    {
        if (WasPrimaryPressedThisFrame())
            CheckHitObject();

        if (IsPrimaryHeld() && objSelected != null)
            DragObject();

        if (WasPrimaryReleasedThisFrame() && objSelected != null)
            DropObject();
    }

    void CheckHitObject()
    {
        if (!TryGetPointerScreen(out var screen))
            return;

        RaycastHit2D hit2D = Physics2D.GetRayIntersection(Camera.main.ScreenPointToRay(screen));
        if (hit2D.collider != null)
        {
            if (hit2D.transform.gameObject.GetComponent<CardControl>() != null)
            {
                if (hit2D.transform.gameObject.GetComponent<CardControl>().IsBack)
                {
                    hit2D.transform.gameObject.GetComponent<CardControl>().FlipCard();
                }
                else
                {
                    objSelected = hit2D.transform.gameObject;
                    Vector2 v2temp = objSelected.transform.position - Camera.main.ScreenToWorldPoint(screen);
                    OffSet = new Vector3(v2temp.x, v2temp.y, 0);
                    objSelected.GetComponent<CardControl>().OnSelect();
                }
            }
        }
    }

    void DragObject()
    {
        if (!TryGetPointerScreen(out var screen))
            return;

        objSelected.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10)) + OffSet;
    }

    void DropObject()
    {
        if (DamageFild.OverlapPoint(objSelected.transform.position))
        {
            objSelected.GetComponent<CardControl>().OnDamage();
            objSelected.GetComponent<CardControl>().SetPoint = DamageFild.transform.position;
        }
        else if (BurnFild.OverlapPoint(objSelected.transform.position))
        {
            objSelected.GetComponent<CardControl>().OnBurn();
            objSelected.GetComponent<CardControl>().SetPoint = BurnFild.transform.position;
        }

        objSelected.GetComponent<CardControl>().OnDrop();
        objSelected = null;
    }

    public void AddCard()
    {
        Instantiate(CardBlank, new Vector3(CreateFild.transform.position.x, CreateFild.transform.position.y, 0), Quaternion.identity);
    }

    static bool TryGetPointerScreen(out Vector3 screen)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            var pos = mouse.position.ReadValue();
            screen = new Vector3(pos.x, pos.y, 0f);
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        screen = Input.mousePosition;
        return true;
#else
        screen = default;
        return false;
#endif
    }

    static bool WasPrimaryPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.wasPressedThisFrame;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    static bool WasPrimaryReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.wasReleasedThisFrame;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonUp(0);
#else
        return false;
#endif
    }

    static bool IsPrimaryHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.isPressed;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#else
        return false;
#endif
    }
}
