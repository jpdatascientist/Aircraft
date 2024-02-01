using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//チェックポイントとフィニッシュポイントとプロベラを回転させる

namespace Aircraft
{
    public class Rotate : MonoBehaviour
    {
        [Tooltip("The speed at which to rotate")]
        public Vector3 rotateSpeed;

        [Tooltip("Whether to randomize the start position")]
        public bool randomize = false;

        // Start is called before the first frame update
        //スタートしたときにtrue
        void Start()
        {
            // Randomize the start position　スタートポジションがセッティング
            if (randomize) transform.Rotate(rotateSpeed.normalized * UnityEngine.Random.Range(0f, 360f));
        }

        // Update is called once per frame　これが１フレームごとに
        void Update()
        {
            transform.Rotate(rotateSpeed * Time.deltaTime, Space.Self);
        }
    }
}
