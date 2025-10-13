using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewMonobehavior : MonoBehaviour {

    protected virtual void Start() {

    }

    protected virtual void Awake() {
        this.LoadComponents();
    }

    protected virtual void Reset() {
        this.LoadComponents();
        this.ResetValue();
    }

    protected virtual void LoadComponents() {
        //Override

    }
    protected virtual void ResetValue() {
        //Override

    }
    protected virtual void OnValidate() {
        //Override
    }

}