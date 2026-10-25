using UnityEngine;

// 泡泡子彈：開槍時就決定打中誰，泡泡只是飛過去的動畫
public class Bubble : MonoBehaviour
{
    const float Speed = 45f;

    Vector3 target;
    Monster monster;
    Vector3 monsterOffset;
    GameManager gm;
    float life = 2f;

    public static void Launch(Vector3 from, Vector3 aimPoint, Monster hitMonster, GameManager gm)
    {
        var go = Shapes.Primitive(PrimitiveType.Sphere, new Color(0.5f, 0.95f, 1f));
        Destroy(go.GetComponent<Collider>());
        go.layer = Shapes.IgnoreRaycastLayer;
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.2f;

        var b = go.AddComponent<Bubble>();
        b.gm = gm;
        b.target = aimPoint;
        b.monster = hitMonster;
        if (hitMonster != null) b.monsterOffset = aimPoint - hitMonster.transform.position;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        // 怪獸會移動，泡泡跟著飛過去
        if (monster != null && monster.Alive) target = monster.transform.position + monsterOffset;

        transform.position = Vector3.MoveTowards(transform.position, target, Speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            if (monster != null && monster.Alive) gm.OnMonsterShot(monster);
            Destroy(gameObject);
        }
    }
}
