using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireChanges : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void ReadComprateString(object data)
    {
        var sensor_data = data as string;
        float changedData = float.Parse(sensor_data) / 400;
        Debug.Log(sensor_data);
        this.gameObject.transform.localScale = new Vector3(changedData, changedData, changedData);
    }
}
