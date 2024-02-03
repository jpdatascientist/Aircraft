using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using System.Linq;

namespace Aircraft
{
    public class AircraftArea : MonoBehaviour
    {
        //ディタ上でフィールドやプロパティにマウスをホバー（重ねる）したときに表示される説明文を指定
        [Tooltip("The path the race will take")]
        public CinemachineSmoothPath racePath;

        [Tooltip("The prefab to use for checkpoints")]
        //checpointprefabというゲームオブジェクトを作る
        public GameObject checkpointprefab;

        [Tooltip("The prefab to use for the start/end checkpoint")]
        public GameObject finishCheckpointPrefab;

        [Tooltip("If true,enable training mode")]
        public bool trainingMode;

        public List<AircraftAgent> AircraftAgents { get; private set; }

        //GameObject型の要素を格納できる動的な配列を表す
        //変数をクラス内外からアクセス可能にし、読み取り=get と書き込み=set(privateはできない) の両方ができる
        // これはインスペクターへ載らない
        public List<GameObject> Checkpoints { get; private set; }

        /// <summary>
        /// Actions to perform when the script wakes up
        /// </summary>

        private void Awake()
        {
            //Aircraftagent配列
            if (AircraftAgents == null) FindAircraftAgents();

        }

        private void FindAircraftAgents()
        {
            //Find all aircraft agents in the area
            //GetComponentsInChildren自身とその子孫オブジェクトから全てのコンポーネントを取得
            AircraftAgents = transform.GetComponentsInChildren<AircraftAgent>().ToList();
            Debug.Assert(AircraftAgents.Count > 0, "NO AircraftAgents found");
        }



        /// Set up the area
        private void Start()
        {
            //Chenckpoints配列
            if (Checkpoints == null) CreateCheckpoints();

        }

        private void CreateCheckpoints()
        {
            //Create checkpoints along race path
            Debug.Assert(racePath != null, "Race Path was not set");

            Checkpoints = new List<GameObject>();

            //numCheckpoints にはレースパスの最大値が入る
            int numCheckpoints = (int)racePath.MaxUnit(CinemachinePathBase.PositionUnits.PathUnits);
            for (int i = 0; i < numCheckpoints; i++)
            {
                // checkpointの宣言
                GameObject checkpoint;

                //finishCheckpointPrefabプレハブを元にして新しいゲームオブジェクトを生成し、その生成されたゲームオブジェクトへの参照をcheckpoint変数に格納
                if (i == numCheckpoints - 1) checkpoint = Instantiate<GameObject>(finishCheckpointPrefab);
                else checkpoint = Instantiate<GameObject>(checkpointprefab);

                //Set the parent,potision,and rotation
                //checkpointの親オブジェクトをracePathのトランスフォームに変更,レースパスに従って進むように設定
                checkpoint.transform.SetParent(racePath.transform);
                checkpoint.transform.localPosition = racePath.m_Waypoints[i].position;
                checkpoint.transform.rotation = racePath.EvaluateOrientationAtUnit(i, CinemachinePathBase.PositionUnits.PathUnits);

                //Add the Checkpoint to the List
                Checkpoints.Add(checkpoint);
            }
        }

        /// <summary>
        /// Resets the position of an agent using its current NextCheckpointIndex, 
        /// unless randomize is true, then will pick a new random checkpoint
        /// </summary>
        /// <param name="agent">The agent to reset</param>
        /// <param name="randomize">If true, will pick a new NextCheckpointIndex before reset</param>

        public void ResetAgentPosition(AircraftAgent agent, bool randomize = false)
        {
            if (AircraftAgents == null) FindAircraftAgents();
            if (Checkpoints == null) CreateCheckpoints();


            if (randomize)
            {
                //  pic up new nest checkpont at rondom
                agent.NextCheckpointIndex = Random.Range(0, Checkpoints.Count);
            }

            // Set start position the previous checkpoint
            int previousCheckpointIndex = agent.NextCheckpointIndex - 1;
            //チェックポイントがスタート位置から始まるように
            if (previousCheckpointIndex == -1) previousCheckpointIndex = Checkpoints.Count - 1;

            float startPosition = racePath.FromPathNativeUnits(previousCheckpointIndex, CinemachinePathBase.PositionUnits.PathUnits);

            // Convert the position on the race path to a position in 3d space
            Vector3 basePosition = racePath.EvaluatePosition(startPosition);

            // Get the orientation at that position on the race path
            Quaternion orientation = racePath.EvaluateOrientation(startPosition);

            // Calculate a horizontal offset so that agents are spread out
            Vector3 positionOffset = Vector3.right * (AircraftAgents.IndexOf(agent) - AircraftAgents.Count / 2f)
                * Random.Range(9f, 10f);


            // Set the aircraft position and rotation
            agent.transform.position = basePosition + orientation * positionOffset;
            agent.transform.rotation = orientation;



        }











    }


}

