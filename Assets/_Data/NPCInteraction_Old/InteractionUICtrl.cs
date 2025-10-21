using UnityEngine;

public class InteractionUICtrl: NewMonobehavior
{
    public Canvas canvas;
    public bool isInteract = false;
    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadCanvas();
    }

    protected virtual void LoadCanvas() { 
        if(canvas != null) return;
        this.canvas = this.transform.parent.GetComponentInChildren<Canvas>();
        Debug.Log(transform.name + ": LoadCanvas: ", gameObject);
    }

    protected virtual void Update() {
        
        if (canvas != null)
            canvas.gameObject.SetActive(isInteract);

        
        isInteract = false;
    }

    public void ToggleCanvas() {
        this.canvas.gameObject.SetActive(true);
    }
}
