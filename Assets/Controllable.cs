using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controllable : MonoBehaviour {



	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private class PlayerControls
    {
        public int Player { get; set; }
        public Dictionary<Controls, List<InputType>> Inputs { get; set; } = new Dictionary<Controls, List<InputType>>();

        public abstract class InputType
        {
            public virtual bool Active { get; }
            public virtual float Value { get; }
            public virtual string Name { get; set; }
        }

        public class KeyboardInput : InputType
        {
            private KeyCode assignedButton;
            private string customButtonName = null;
            public override bool Active => Input.GetKeyDown(assignedButton);
            public override float Value => 1f;
            public override string Name
            {
                get
                {
                    return customButtonName ?? assignedButton.ToString();
                }
                set
                {
                    customButtonName = value;
                }
            }
        }
    }
}

public enum Controls
{
    Up,
    Down,
    Left,
    Right,
    Forward,
    Backward,
    Jump,
    Pause,
    Inventory,
    Map,
    Confirm,
    Cancel,
    LightAttack,
    HeavyAttack,
    Aim,
    Fire,
    WeaponSelect,
    Defend,
    Crouch,
    Dash,
    Dodge,
    Lockon,
    Glide,

    // Change as needed
}
