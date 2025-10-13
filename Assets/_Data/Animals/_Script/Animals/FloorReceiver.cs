using GrabQuiz.Animals;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FloorReceiver : NewMonobehavior {
    public BoxCollider boxCollider;

    private void OnTriggerEnter( Collider other ) {
        /*Debug.Log("OnTriggerEnter: " + other.gameObject.name, gameObject);*/
        AnimalFoodSender animalFood = other.gameObject.GetComponentInChildren<AnimalFoodSender>();

        if (animalFood == null) return;

        StartCoroutine(RespawnAfterDelay(animalFood));
    }

    private IEnumerator RespawnAfterDelay( AnimalFoodSender animalFood ) {
        yield return new WaitForSeconds(5f);

        animalFood.Respawn();
    }

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadSphereCollider();
    }

    protected virtual void LoadSphereCollider() {
        if (this.boxCollider != null) return;
        this.boxCollider = this.GetComponent<BoxCollider>();
        Debug.Log(transform.name + " LoadSphereCollider: " + this.boxCollider);
    }
}
