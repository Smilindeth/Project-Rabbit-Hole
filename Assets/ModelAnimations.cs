using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModelAnimations : MonoBehaviour {

    protected JumpableCharacter JumpableParent;

	// Use this for initialization
	protected void Start () {
        JumpableParent = GetComponentInParent(typeof(JumpableCharacter)) as JumpableCharacter;
	}
	
	// Update is called once per frame
	protected void Update () {
        // TODO: Swap to local rotation
        if ((JumpableParent?.CurrentJumpState & JumpableCharacter.JumpState.Spinning) > 0)
        {
            // May need flipping
            if (JumpableParent.JumpDirection.x < 0)
                transform.Rotate(Vector3.forward, 180 * Time.deltaTime);
            if (JumpableParent.JumpDirection.x > 0)
                transform.Rotate(Vector3.back, 180 * Time.deltaTime);
            if (JumpableParent.JumpDirection.z < 0)
                transform.Rotate(Vector3.left, 180 * Time.deltaTime);
            if (JumpableParent.JumpDirection.z > 0)
                transform.Rotate(Vector3.right, 180 * Time.deltaTime);
        } else if (transform.rotation.z != 0)
            transform.rotation = new Quaternion(transform.rotation.x, transform.rotation.y, 0, transform.rotation.w);
    }
}
