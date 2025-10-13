using UnityEngine;

public class SpawnPosition : NewMonobehavior
{
    protected override void Start() {
        base.Start();
        this.gameObject.SetActive(false);
    }
}
