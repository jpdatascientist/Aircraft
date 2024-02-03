using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;
using Unity.MLAgents.Policies;


namespace Aircraft
{
    public class AircraftAgent : Agent
    {
        [Header("Movement Parameters")]
        public float thrust = 100000f; //Aircraftを前に進ませるスピード　Z軸
        public float pitchSpeed = 100f;//羽も動き、傾きなど
        public float yawSpeed = 100f;
        public float rollSpeed = 100f;
        public float boostMultiplier = 2f;

        [Header("Explosion Stuff")]
        [Tooltip("The aircraft mesh that will disappear on explosion")]
        public GameObject meshObject;

        [Tooltip("The game object of the explosion particle effect")]
        public GameObject explosionEffect;

        [Header("Training")]
        [Tooltip("Number of steps to time out after in training")]
        public int stepTimeout = 300;


        // これはインスペクターへ載らない
        public int NextCheckpointIndex { get; set; }

        private AircraftArea area;
        new private Rigidbody rigidbody;
        private TrailRenderer trail;


        // When the next step timeout will be during training
        private float nextStepTimeout;

        // Whether the aircraft is frozen (intentionally not flying)
        private bool frozen = false;



        // Controls
        private float pitchChange = 0f;
        private float smoothPitchChange = 0f;
        private float maxPitchAngle = 45f;
        private float yawChange = 0f;
        private float smoothYawChange = 0f;
        private float rollChange = 0f;
        private float smoothRollChange = 0f;
        private float maxRollAngle = 45f;
        private bool boost;



        /// <summary>
        /// Called when the agent is first initialized　エージェントの初期位置や初期状態の設定
        /// </summary>
        public override void Initialize()
        {
            area = GetComponentInParent<AircraftArea>();
            rigidbody = GetComponent<Rigidbody>();
            trail = GetComponent<TrailRenderer>();

            // Override the max step set in the inspector
            // Max 5000 steps if training, infinite steps if racing
            //もしtrainigmodeがtrueであれば5000が代入　5000に達したら終了
            MaxStep = area.trainingMode ? 5000 : 0;

        }

        /// <summary>
        /// Called when a new episode begins
        /// </summary>
        public override void OnEpisodeBegin()
        {
            // Reset the velocity, position, and orientation
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            trail.emitting = false;
            // this= AircraftAgent,area.trainigMode = Trueであればtrainigmodeになる
            area.ResetAgentPosition(agent: this, randomize: area.trainingMode);

            // Update the step timeout if training
            // StepCount = 0,stepTimeout = 300,nextStepCount = 300
            if (area.trainingMode) nextStepTimeout = StepCount + stepTimeout;

        }




        /// <summary>
        /// Read action inputs from vectorAction 決定→行動         
        /// </summary>
        /// <param name = "vectorAction" > The chosen actions</param>
        /// ビデオではfloatとしていたが、引数の書き方はこれでないとoverrideできない      

        public override void OnActionReceived(ActionBuffers vectorAction)
        {
            if (frozen) return;


            // vectorAction
            // Read values for pitch and yaw
            pitchChange = vectorAction.ContinuousActions[0]; // up or none
            if (pitchChange == 2) pitchChange = -1f; // down
            yawChange = vectorAction.ContinuousActions[1]; // turn right or none
            if (yawChange == 2) yawChange = -1f; // turn left

            // Read value for boost and enable/disable trail renderer
            boost = vectorAction.ContinuousActions[2] == 1;
            if (boost && !trail.emitting) trail.Clear();
            trail.emitting = boost;

            ProcessMovement();

            // Small negative reward every step
            AddReward(-1f / MaxStep);

            // Make sure we haven't run out of time if training タイムアウトしてしまったらマイナス
            if (StepCount > nextStepTimeout)
            {
                AddReward(-.5f);
                EndEpisode();
            }

            //nextcheckpointへ到着したら今の場所が　localCheckpointDirへ
            Vector3 localCheckpointDir = VectorToNextCheckpoint();
            //もし次のチェックポイントへのベクトルが小さかったらチェックポイント到達
            if (localCheckpointDir.magnitude < Academy.Instance.EnvironmentParameters.GetWithDefault("checkpoint_radius", 0f))
            {
                GotCheckpoint();
            }
        }


        /// <summary>
        /// Prevent the agent from moving and taking actions,Agentが動くのを防ぐ
        /// </summary>
        public void FreezeAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in trainig");
            frozen = true;
            rigidbody.Sleep();
            trail.emitting = false;
        }

        /// <summary>
        /// Resume agent movement and actions,Agentを復活させる
        /// </summary>
        public void ThawAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in trainig");
            frozen = false;
            rigidbody.WakeUp();
        }





        /// <summary>
        /// Gets a vector to the next checkpoint the agent needs to fly through　
        /// </summary>
        /// <returns>A local-space vector</returns>
        private Vector3 VectorToNextCheckpoint()
        {
            Vector3 nextCheckpointDir = area.Checkpoints[NextCheckpointIndex].transform.position - transform.position;
            Vector3 localCheckpointDir = transform.InverseTransformDirection(nextCheckpointDir);
            return localCheckpointDir;
        }

        /// <summary>
        /// Called when the agent flies through the correct checkpoint     
        /// </summary>
        private void GotCheckpoint()
        {
            // Next checkpoint reached, update
            NextCheckpointIndex = (NextCheckpointIndex + 1) % area.Checkpoints.Count;

            if (area.trainingMode)
            {
                AddReward(.5f);
                nextStepTimeout = StepCount + stepTimeout;
            }
        }




        /// <summary>
        /// Calculate and apply movement
        /// </summary>

        private void ProcessMovement()
        {

            // Calculate boost
            float boostModifier = boost ? boostMultiplier : 1f;

            // Apply forward thrust
            rigidbody.AddForce(transform.forward * thrust * boostModifier, ForceMode.Force);

            // Get the current rotation
            Vector3 curRot = transform.rotation.eulerAngles;

            // Calculate the roll angle (between -180 and 180)
            float rollAngle = curRot.z > 180f ? curRot.z - 360f : curRot.z;
            if (yawChange == 0f)
            {
            // Not turning; smoothly roll toward center
                rollChange = -rollAngle / maxRollAngle;
            }
            else
            {
                // Turning; roll in opposite direction of turn
                rollChange = -yawChange;
            }

            //culculate smooth deltas
            smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
            smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
            smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);

            // Calculate new pitch, yaw, and roll. Clamp pitch and roll.
            float pitch = curRot.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

            float yaw = curRot.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

            float roll = curRot.z + smoothRollChange * Time.fixedDeltaTime * rollSpeed;
            if (roll > 180f) roll -= 360f;
            roll = Mathf.Clamp(roll, -maxRollAngle, maxRollAngle);

            // Set the new rotation
            transform.rotation = Quaternion.Euler(pitch, yaw, roll);

        }


        /// <summary>
        /// React to entering a trigger
        /// </summary>
        /// <param name="other">The collider entered</param>
        private void OnTriggerEnter(Collider other)
        {
            if (other.transform.CompareTag("checkpoint") &&
                other.gameObject == area.Checkpoints[NextCheckpointIndex])
            {
                GotCheckpoint();
            }   


        }


        /// <summary>
        /// React to collisions
        /// </summary>
        /// <param name="collision">Collision info</param>
        private void OnCollisionEnter(Collision collision)
        {
            // もし衝突したのが、他の飛行機ではなかった場合
            if (!collision.transform.CompareTag("agent"))
            {
                // もしtrainigModeであれば
                if (area.trainingMode)
                {
                    AddReward(-1f);
                    EndEpisode();
                }
                else
                {
                    StartCoroutine(ExplosionReset());
                }
            }
        }


        /// <summary>
        /// Resets the aircraft to the most recent complete checkpoint
        /// </summary>
        /// <returns>yield return</returns>
        /// コルーチンメソッドを使用してエージェントの爆発リセットを実装している　一連の処理を時間の経過とともに行う
        private IEnumerator ExplosionReset()
        {
            // Agent凍結
            FreezeAgent();

            // Disable aircraft mesh object, enable explosion
            // AgentのmeshObjectを非アクティブにて外観を爆発エフェクトへ
            meshObject.SetActive(false);
            explosionEffect.SetActive(true);
            // 2秒間の待機
            yield return new WaitForSeconds(2f);

            // Disable explosion, re-enable aircraft mesh
            meshObject.SetActive(true);
            explosionEffect.SetActive(false);

            // Reset position
            area.ResetAgentPosition(agent: this);
            yield return new WaitForSeconds(1f);

            ThawAgent();
        }




    }
}



