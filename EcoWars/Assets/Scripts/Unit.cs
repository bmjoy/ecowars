﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//Handles the target-based movement of units
[RequireComponent(typeof(Rigidbody))]
public class Unit : MonoBehaviour {

    [System.NonSerialized] public Transform target; //unit moves towards target
    public float minDistance = 3f;//stops moving when minDistance reached
    public float maxDistance = 20f; //only follow targets maxDistance appart
    public float speed = 1f;
    public float walkAnimationSpeed = 10f;
    public float eatAnimationSpeed = 1f;
    public float animationTilt = 10f;
    [System.NonSerialized] public Rigidbody rb;
    public float maxHealth = 10f;
    [System.NonSerialized] public float health;
    [System.NonSerialized] public bool isBeingOverride;
    [System.NonSerialized] public Vector3 destination = Vector3.zero;
    [System.NonSerialized] public GravityAttractor planet;
    [System.NonSerialized] public UnitState unitState;
    [System.NonSerialized] public float destinationStampTime;
    public float foodRange = 5f;
    [SerializeField] public float attackDamagePerSecond = 1f;
    public float originalEatRange = 1f;
    [SerializeField] public float enemyDetectionRange = 10f;
    public float originalAttackRange = 2f;
    public float attackRange = 2f;
    [System.NonSerialized] public float eatRange = 1f;
    [System.NonSerialized] public string enemyTag;
    public float stomachSize = 10f;
    public float stomachDecreasePerSecond = 0.1f;
    [System.NonSerialized] public float stomachFilledAmount = 10f; //how much of the stomach is filled
    public float hungerThreshold = 5f;
    public float hungerDamage = 0.1f;
    public float rotationSpeed = 2f;

    // void (modifier) functions at the top #########################################################################

    public void Awake() {
        if (transform.tag == "Pet") {
            enemyTag = "Hostile";
        } else if (transform.tag == "Hostile") {
            enemyTag = "Pet";
        } else {
            throw new System.Exception("Wrong tag for unit");
        }
        health = maxHealth;
        isBeingOverride = false;
        planet = GameObject.FindGameObjectWithTag("Planet").GetComponent<GravityAttractor>();
        GetComponent<Rigidbody>().useGravity = false; //deactivate built-in downwards gravity
        GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb = GetComponent<Rigidbody>();
    }

    private void Update() {
        unitState = UnitStateMachine.NextState(this);
    }

    //Rotate, move unit towards destination, affect gravity and animate
    public void Move(Vector3 destination) {
        //only rotate normal to the planet
        Vector3 projectedDestination = Vector3.ProjectOnPlane(destination, transform.up);
        Quaternion targetRotation = Quaternion.LookRotation(projectedDestination, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);

        //move forward in the local axis
        rb.MovePosition(rb.position + transform.forward * Time.fixedDeltaTime * speed);

        //affect gravity
        planet.Attract(transform);

        //animate movement
        AnimateWalk();
    }

    //Find a random point in planet's surface 
    public void GetDestination() {
        //random position somewhere on the surface of the planet
        destination = UnityEngine.Random.onUnitSphere * 5f;
        destinationStampTime = Time.time;
    }

    public void TakeDamage(float damage) {
        health -= damage;
        if (health <= 0) { this.Die(); }
    }

    //TODO: REPLACE BY BLENDER-MADE ANIMATION SET UP IN ANIMATION HANDLER
    public void AnimateWalk() {
        Transform prefab = this.gameObject.transform.GetChild(0);

        //reset position from eat animation
        prefab.localPosition = Vector3.Lerp(prefab.localPosition, new Vector3(0f, 0f, 0f), Time.deltaTime);

        //the z rotation goes frrom -animationTilt to animationTilt according to a sine.
        float currentRotation = Mathf.Sin(Time.time * walkAnimationSpeed) * animationTilt;
        prefab.localRotation = Quaternion.Euler(new Vector3(0, 0, currentRotation));
        return;
    }

    //TODO: REPLACE BY BLENDER-MADE ANIMATION SET UP IN ANIMATION HANDLER
    public void AnimateEat() {
        Transform prefab = this.gameObject.transform.GetChild(0);

        //reset rotation from walk animation
        prefab.localRotation = Quaternion.Lerp(prefab.localRotation, Quaternion.Euler(new Vector3(0, 0, 0)), Time.deltaTime);

        //jump animation
        float jumpHeight = Mathf.Abs(Mathf.Cos(Time.time * eatAnimationSpeed) * 2f) - .5f;
        prefab.localPosition = Vector3.Lerp(prefab.localPosition, new Vector3(0f, jumpHeight, 0f), Time.deltaTime * eatAnimationSpeed);
        return;
    }

    public void OverrideDestination(Vector3 newDestination) {
        minDistance = 1f; //update minDistance as new destinations are more accurate
        destination = newDestination;
        isBeingOverride = true;
        unitState = UnitState.Wander;
        //todo: override to false;
    }

    public void HungerEffect() {
        this.stomachFilledAmount -= this.stomachDecreasePerSecond * Time.fixedDeltaTime;
        if (this.stomachFilledAmount <= 0) { this.TakeDamage(this.hungerDamage); }
    }

    public void ThirstEffect() {
        // suffer thirst
    }

    public void Die() {
        unitState = UnitState.Dead;
    }

    // query functions here (not allowed to modify state) ##################################################

    public bool IsThreatened() {
        // check for near enemies
        return false;
    }

    public bool ShouldBeAggressive() {
        // randomly return true or false based on aggression
        return true;
    }

    public bool IsThirsty() {
        return false;
    }

    public bool IsVeryThirsty() {
        return false;
    }

    //does the unit require to be given a new destination
    public bool NeedsDestination() {
        //no destination TODO: check maybe null
        if (destination == Vector3.zero) { return true; }

        ////destination already reached
        if ((destination - transform.position).magnitude <= minDistance) { return true; }

        //if its wandering and couldn't reach the destination in 10 sec reset 
        if (Time.time - destinationStampTime > 10f && unitState == UnitState.Wander) { return true; }

        //otherwise
        return false;
    }

    //Check for enemy units in range
    public Unit CheckForEnemy() {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        //find closest enemy and return it
        float closestDistance = Mathf.Infinity;
        Unit closestEnemy = null;
        foreach (GameObject enemy in enemies) {
            float distance = (transform.position - enemy.transform.position).magnitude;
            if (distance < enemyDetectionRange && distance < closestDistance) {
                closestDistance = distance;
                closestEnemy = enemy.GetComponent<Unit>();
            }
        }
        return closestEnemy;
    }

    //Check for enemy units in range
    public Food CheckForFood() {
        if (stomachFilledAmount > hungerThreshold) { return null; } //don't look for food if not hungry
        //TODO: check for efficiency. Is it iterating through all gameobjects in scene?
        GameObject[] foods = GameObject.FindGameObjectsWithTag("Food");

        //find closest source of food and return it
        float closestDistance = Mathf.Infinity;
        Food closestFood = null;
        foreach (GameObject food in foods) {
            float distance = (transform.position - food.transform.position).magnitude;
            if (distance < foodRange && distance < closestDistance) {
                closestDistance = distance;
                closestFood = food.GetComponent<Food>();
            }
        }

        return closestFood;
    }
}