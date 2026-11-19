using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.Reflection;
using OrbCombat;
public static class MeetingVerification
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/OrbCombat/Scenes/Level02_MeetingRoom.unity");
        var game = UnityEngine.Object.FindFirstObjectByType<OfficeEncounter>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var t = game.GetType();
        void Set(string key, object value) => t.GetField(key,flags).SetValue(game,value);
        object Get(string key) => t.GetField(key,flags).GetValue(game);
        object Call(string key, params object[] args) => t.GetMethod(key,flags).Invoke(game,args);
        void Check(bool value,string label) { if(!value) throw new Exception(label); Debug.Log("PASS "+label); }
        Call("Start");
        var e = game.enemies[0];
        Check(game.rotatingTable && game.manager && game.reboundScreen && game.contactHazards.Length == 9,"meeting scene bindings");
        foreach(float side in new[]{-1f,1f})
        {
            Call("ResetEncounter");
            game.orb.position = new Vector3(side*3,.7f,-1);
            Set("flying",true); Set("heavy",true); Set("direction",Vector3.forward);
            Call("StepOrb",.15f);
            Check((float)Get("turnRemaining") * side < 0, "heavy throw torque "+side);
            e.position = new Vector3(side*3,.5f,2f);
            for(int n=0;n<12;n++) Call("TurnTable",.02f);
            Check(((Vector3[])Get("impulses"))[0].z>1,"table sweep "+side);
        }
        Call("ResetEncounter"); Set("started",true);
        e.position = new Vector3(2,.5f,5.36f);
        ((Vector3[])Get("impulses"))[0] = Vector3.forward*15;
        Call("FixedUpdate");
        Check(((Vector3[])Get("impulses"))[0].z<0,"screen rebound");
        foreach(var c in new[]{game.contactHazards[0],game.contactHazards[3]})
        {
            Call("ResetEncounter"); Set("started",true);
            var p=c.bounds.center + Vector3.back * (c.bounds.extents.z + .501f);
            e.position=new Vector3(p.x,.5f,p.z);
            Call("FixedUpdate"); Check(((int[])Get("enemyHp"))[0]==2,"enemy hazard damage "+c.name);
        }
        Call("ResetEncounter");
        Vector3 goal = new Vector3(-10.54f,.35f,-2);
        Check(((Vector3)Call("Next",e.position,goal)-e.position).sqrMagnitude>.0001f,"route to wall");
        game.player.position=game.mouths[0].position;
        Set("started",true);Call("FixedUpdate");Check((int)Get("health")==0,"CEO mouth lethal");
        Debug.Log("MEETING VERIFIED");
    }
}
