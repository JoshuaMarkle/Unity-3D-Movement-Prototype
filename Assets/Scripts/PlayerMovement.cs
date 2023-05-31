using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour {

    // --------------------------------------------------------------------------------- Variables ------------------------------------------------------------------------ //
    #region Variables
        #region Assingables
            [Header("Assingables")]
            public Transform playerCam;
            public Transform orientation;
            public Transform headPos;
            public Camera cam;
            private Rigidbody rb;
            public ParticleSystem landingParticles;
            public Transform ragdollBody;
        #endregion

        #region Movement Variables
            [Header("Movement")]
            [Range(2000.0f, 6000.0f)]
            public float moveSpeed = 4500;                          // How fast can the player accelerate?
            [Range(0.0f, 30.0f)]
            public float maxSpeed = 20;                             // Speed cap
            [Range(0.0f, 2.0f)]
            public float counterMovement = 0.175f;
            private float threshold = 0.01f;
            [Range(25.0f, 45.0f)]
            public float maxSlopeAngle = 35f;                       // Max angle with ground that allows the player to move
            public bool grounded;                                   // Is the player on the ground?
            public LayerMask whatIsGround;                          // What layer is the ground on?

            // Jump Sequence
            [Space]
            [Range(0.0f, 1000.0f)]
            public float jumpForce = 550f;                          // How hard you push off the ground
            private float jumpCooldown = 0.25f;                     // Time before jumps
            private bool readyToJump = true;                        // Can you jump?

            // Landing Sequence
            [Space]
            public AnimationCurve landCurve;                        // Landing dip motion
            public AnimationCurve landForceCurve;                   // How much do you dip on landing?
            [Range(0.0f, 2.0f)]
            public float landTime = 1f;                             // How long do you time does the landing anim take?
            private float landProgress = 0f;
            private float landForce = 0f;                           // How hard do you land?
            private float maxDownSpeed = 10f;                       // Max dip speed
            private bool startLand = false;
            private bool strafe = false;                            // Do you jump out of a landing anim?

            // Sliding & Crouching
            [Space]
            private Vector3 crouchScale = new Vector3(1, 0.5f, 1);  // Size of player while crouching
            private Vector3 playerScale;                            // Normal player size
            [Range(0.0f, 1000.0f)]
            public float slideForce = 400;                          // How hard do you start your slide?
            public float slideCounterMovement = 0.2f;
            private Vector3 normalVector = Vector3.up;
            private Vector3 wallNormalVector;

            // Input
            private float x, y;                                     // Move direction
            private bool jumping, sprinting, crouching;
        #endregion

        #region Wall Run Variables
            [Header("Wall Running")]
            [Range(0.0f, 2.0f)]
            public float wallRunGravity;          // How hard you fall down the wall?
            public float wallRunJumpForce;        // How hard the player
            public float wallRunInitialForce;     // On contact force
            public bool wallRunCanImpulse;
            public float wallDistance = .5f;      // Wall distance
            public float minimumJumpHeight = 1.5f;

            public float tilt { get; private set; }

            private bool wallLeft = false;
            private bool wallRight = false;

            RaycastHit leftWallHit;
            RaycastHit rightWallHit;
        #endregion

        #region Camera Variables
            [Header("Camera")]
            private float xRotation;
            public float sensitivity = 100f;      // Look sensitivity
            public float sensMultiplier = 1f;
            public float sensitivitySmoother;     // Makes looking feel smoother
            [Range(60.0f, 120.0f)]
            public float fov;                     // Normal fov
            [Range(60.0f, 120.0f)]
            public float wallRunfov;              // Fast fov
            public float wallRunfovTime;          // Fov travel time
            [Range(0.0f, 15.0f)]
            public float camTilt;                 // Wall run head tilt
            public float camTiltTime;             // Tilt travel time
        #endregion

        #region Leaning Variables
            [Header("Leaning")]
            [Range(0.0f, 2.0f)]
            public float rotAmount = 10f;         // Head tilt on movement
            [Range(0.0f, 10.0f)]
            public float smoothRotAmount = 0f;    // Smooth head tilt

            // Right and loft handler
            private float currRotZ;
            private float rotTimeZ = 0f;
            private float rotZ = 0f;
            private float prevRotZ = 0f;
            private float initialStartZ = 0f;

            // Forward and background handler
            private float currRotX;
            private float rotTimeX = 0f;
            private float rotX = 0f;
            private float prevRotX = 0f;
            private float initialStartX = 0f;
        #endregion
    #endregion

    // -------------------------------------------------------------------------------- Core Functions ------------------------------------------------------------------- //
    #region Start & Awake
        void Awake() {
            rb = GetComponent<Rigidbody>();
        }

        void Start() {
            playerScale =  transform.localScale;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    #endregion

    #region Updates
        private void FixedUpdate() {
            Movement();
            Landing();
        }

        private void Update() {
            MyInput();
            Look();
            HeadTilt();
            CheckWall();

            ragdollBody.rotation = Quaternion.Euler(0, desiredX, 0);
            ragdollBody.position = cam.transform.position;

            // // Increase fov - i n e f f e c t i v e
            // if(Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0) {
            //     wallRunfovTime += Time.deltaTime;
            //     cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, wallRunfov, wallRunfovTime);
            // } else {
            //     cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, wallRunfovTime);
            // }
        }
    #endregion

    // --------------------------------------------------------------------------------- Functions ----------------------------------------------------------------------- //
    #region Player Actions
        #region Input
            private void MyInput() {
                x = Input.GetAxisRaw("Horizontal");
                y = Input.GetAxisRaw("Vertical");
                jumping = Input.GetButton("Jump");
                crouching = Input.GetKey(KeyCode.LeftControl);

                //Crouching
                if (Input.GetKeyDown(KeyCode.LeftControl))
                    StartCrouch();
                if (Input.GetKeyUp(KeyCode.LeftControl))
                    StopCrouch();
            }
        #endregion

        #region Looking
            #region Basic Look
                private float desiredX;
                private void Look() {
                    float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
                    float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

                    //Find current look rotation
                    Vector3 rot = playerCam.transform.localRotation.eulerAngles;
                    desiredX = rot.y + mouseX;

                    //Rotate, and also make sure we dont over- or under-rotate.
                    xRotation -= mouseY;
                    xRotation = Mathf.Clamp(xRotation, -90f, 90f);

                    //Perform the rotations
                    playerCam.transform.localRotation = Quaternion.Euler(xRotation + currRotX, desiredX, currRotZ + tilt);
                    // playerCam.transform.localRotation = Quaternion.Slerp(playerCam.transform.localRotation, Quaternion.Euler(xRotation + currRotX, desiredX, currRotZ + tilt), Time.deltaTime * sensitivitySmoother);
                    orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0);

                }

                public Vector2 FindVelRelativeToLook() {
                    float lookAngle = orientation.transform.eulerAngles.y;
                    float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

                    float u = Mathf.DeltaAngle(lookAngle, moveAngle);
                    float v = 90 - u;

                    float magnitue = rb.velocity.magnitude;
                    float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
                    float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);

                    return new Vector2(xMag, yMag);
                }
            #endregion

            #region HeadTilt
                private void HeadTilt() {
                    // Lean right and left handler
                    rotZ = -Input.GetAxisRaw("Horizontal") * rotAmount;
                    if(prevRotZ == rotZ) {
                        rotTimeZ += Time.deltaTime * smoothRotAmount;
                        currRotZ = Mathf.Lerp(initialStartZ, rotZ, rotTimeZ);
                    } else {
                        rotTimeZ = 0f;
                        initialStartZ = currRotZ;
                        prevRotZ = rotZ;
                    }

                    // Lean up and down handler
                    rotX = Input.GetAxisRaw("Vertical") * rotAmount;
                    if(prevRotX == rotX) {
                        rotTimeX += Time.deltaTime * smoothRotAmount;
                        currRotX = Mathf.Lerp(initialStartX, rotX, rotTimeX);
                    } else {
                        rotTimeX = 0f;
                        initialStartX = currRotX;
                        prevRotX = rotX;
                    }
                }
            #endregion
        #endregion

        #region Simple Movement
            private void Movement() {
                //Extra gravity
                rb.AddForce(Vector3.down * Time.deltaTime * 10);

                //Find actual velocity relative to where player is looking
                Vector2 mag = FindVelRelativeToLook();
                float xMag = mag.x, yMag = mag.y;

                //Counteract sliding and sloppy movement
                CounterMovement(x, y, mag);

                //If holding jump && ready to jump, then jump
                if (readyToJump && jumping) Jump();

                //Set max speed
                float maxSpeed = this.maxSpeed;

                //If sliding down a ramp, add force down so player stays grounded and also builds speed
                if (crouching && grounded && readyToJump) {
                    rb.AddForce(Vector3.down * Time.deltaTime * 3000);
                    return;
                }

                //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
                if (x > 0 && xMag > maxSpeed) x = 0;
                if (x < 0 && xMag < -maxSpeed) x = 0;
                if (y > 0 && yMag > maxSpeed) y = 0;
                if (y < 0 && yMag < -maxSpeed) y = 0;

                //Some multipliers
                float multiplier = 1f, multiplierV = 1f;

                // Movement in air
                if (!grounded) {
                    multiplier = 0.5f;
                    multiplierV = 0.5f;
                }

                // Movement while sliding
                if (grounded && crouching) multiplierV = 0f;

                //Apply forces to move player
                rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
                rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * multiplier);
            }

            private void CounterMovement(float x, float y, Vector2 mag) {
                if (!grounded || jumping) return;

                //Slow down sliding
                if (crouching) {
                    rb.AddForce(moveSpeed * Time.deltaTime * -rb.velocity.normalized * slideCounterMovement);
                    return;
                }

                //Counter movement
                if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0)) {
                    rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
                }
                if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0)) {
                    rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
                }

                //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
                if (Mathf.Sqrt((Mathf.Pow(rb.velocity.x, 2) + Mathf.Pow(rb.velocity.z, 2))) > maxSpeed) {
                    float fallspeed = rb.velocity.y;
                    Vector3 n = rb.velocity.normalized * maxSpeed;
                    rb.velocity = new Vector3(n.x, fallspeed, n.z);
                }
            }
        #endregion

        #region Jumping
            private void Jump() {
                if (grounded && readyToJump) {
                    readyToJump = false;

                    //Add jump forces
                    rb.AddForce(Vector2.up * jumpForce * 1.5f);
                    rb.AddForce(normalVector * jumpForce * 0.5f);

                    //If jumping while falling, reset y velocity.
                    Vector3 vel = rb.velocity;
                    if (rb.velocity.y < 0.5f)
                        rb.velocity = new Vector3(vel.x, 0, vel.z);
                    else if (rb.velocity.y > 0)
                        rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);

                    Invoke(nameof(ResetJump), jumpCooldown);
                }
            }

            private void ResetJump() {
                readyToJump = true;
            }
        #endregion

        #region Crouching
            private void StartCrouch() {
                transform.localScale = crouchScale;
                transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
                if(rb.velocity.magnitude > 0.5f) {
                    if(grounded) {
                        rb.AddForce(orientation.transform.forward * slideForce);
                    }
                }
            }

            private void StopCrouch() {
                transform.localScale = playerScale;
                transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
            }
        #endregion

        #region Ground Check
            private bool IsFloor(Vector3 v) {
                float angle = Vector3.Angle(Vector3.up, v);
                return angle < maxSlopeAngle;
            }

            private bool cancellingGrounded;

            private void OnCollisionStay(Collision other) {
                //Make sure we are only checking for walkable layers
                int layer = other.gameObject.layer;
                if (whatIsGround != (whatIsGround | (1 << layer))) return;

                //Iterate through every collision in a physics update
                for (int i = 0; i < other.contactCount; i++) {
                    Vector3 normal = other.contacts[i].normal;
                    //FLOOR
                    if (IsFloor(normal)) {
                        grounded = true;
                        cancellingGrounded = false;
                        normalVector = normal;
                        CancelInvoke(nameof(StopGrounded));
                    }
                }

                //Invoke ground/wall cancel, since we can't check normals with CollisionExit
                float delay = 3f;
                if (!cancellingGrounded) {
                    cancellingGrounded = true;
                    Invoke(nameof(StopGrounded), Time.deltaTime * delay);
                }
            }

            private void StopGrounded() {
                grounded = false;
            }
        #endregion

        #region Landing Sequence
            private void Landing() {
                // Calculate the landing force
                if(rb.velocity.y <= -1) {
                    landForce = landForceCurve.Evaluate(-rb.velocity.y / maxDownSpeed);
                }

                // Start a procedural landing animation
                if(startLand && !Input.GetButton("Jump") && !strafe) {
                    landProgress += Time.deltaTime;
                    if(landProgress <= landTime) {
                        playerCam.transform.position = new Vector3(headPos.position.x, headPos.position.y + (-landCurve.Evaluate(landProgress / landTime) * landForce), headPos.position.z);
                    } else {
                        startLand = false;
                    }
                } else if(startLand) {
                    strafe = true;
                    landProgress += Time.deltaTime;
                    if(landProgress <= landTime) {
                        playerCam.transform.position = new Vector3(headPos.position.x, Mathf.Lerp(playerCam.transform.position.y, headPos.position.y, landProgress / landTime), headPos.position.z);
                    } else {
                        startLand = false;
                    }
                } else {
                    playerCam.transform.position = headPos.position;
                }
            }

            private void OnCollisionEnter(Collision col) {
                if(col.collider.gameObject.layer == 3 && !wallLeft && !wallRight) {
                    if(!Input.GetButton("Jump")) {
                        startLand = true;
                        strafe = false;
                    }
                    landProgress = 0f;
                    if(landForce >= 0.8f) {
                        landingParticles.Play();
                    }
                }
            }
        #endregion

        #region Wall Running
            bool CanWallRun() {
                return !Physics.Raycast(transform.position, Vector3.down, minimumJumpHeight);
            }

            void CheckWall() {
                wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallHit, wallDistance);
                wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallHit, wallDistance);

                if(CanWallRun()) {
                    if(wallLeft) {
                        StartWallRun();
                    }
                    else if(wallRight) {
                        StartWallRun();
                    } else {
                        StopWallRun();
                    }
                } else {
                    StopWallRun();
                }
            }

            void StartWallRun() {
                rb.useGravity = true;
                rb.AddForce(Vector3.up * wallRunGravity, ForceMode.Force);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, wallRunfov, wallRunfovTime * Time.deltaTime);

                if(wallLeft) {
                    tilt = Mathf.Lerp(tilt, -camTilt, camTiltTime * Time.deltaTime);
                }
                else if(wallRight) {
                    tilt = Mathf.Lerp(tilt, camTilt, camTiltTime * Time.deltaTime);
                }

                if(wallRunCanImpulse) {
                    rb.AddForce(Vector3.down * wallRunInitialForce, ForceMode.Impulse);
                    wallRunCanImpulse = false;
                }

                if(Input.GetKeyDown(KeyCode.Space)) {
                    if(wallLeft) {
                        Vector3 wallRunJumpDirection = transform.up + leftWallHit.normal;
                        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
                        rb.AddForce(wallRunJumpDirection * wallRunJumpForce * 100, ForceMode.Force);
                    }
                    else if(wallRight) {
                        Vector3 wallRunJumpDirection = transform.up + rightWallHit.normal;
                        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
                        rb.AddForce(wallRunJumpDirection * wallRunJumpForce * 100, ForceMode.Force);
                    }
                }
            }

            void StopWallRun() {
                rb.useGravity = true;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, wallRunfovTime * Time.deltaTime);
                tilt = Mathf.Lerp(tilt, 0, camTiltTime * Time.deltaTime);
            }
        #endregion
    #endregion
}
