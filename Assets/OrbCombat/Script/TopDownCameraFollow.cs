
using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Settings")]
    public float height = 18f;
    public float smoothTime = 0.25f;

    [Header("Subtle Camera Sway")]
    public bool enableSway = true;
    public float swayAmount = 0.06f;
    public float swaySpeed = 5f;
    public float rotationSway = 0.3f;

    private Vector3 velocity;
    private Vector3 previousTargetPosition;
    private float swayWeight;

    void Start()
    {
        if (target == null) return;

        transform.position = new Vector3(
            target.position.x,
            height,
            target.position.z
        );

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        previousTargetPosition = target.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = new Vector3(
            target.position.x,
            height,
            target.position.z
        );

        Vector3 smoothPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime
        );

        float movementSpeed =
            Vector3.Distance(target.position, previousTargetPosition)
            / Mathf.Max(Time.deltaTime, 0.0001f);

        float desiredWeight = enableSway
            ? Mathf.Clamp01(movementSpeed / 5f)
            : 0f;

        swayWeight = Mathf.MoveTowards(
            swayWeight,
            desiredWeight,
            Time.deltaTime * 4f
        );

        float wave = Mathf.Sin(Time.time * swaySpeed);

        Vector3 swayOffset = new Vector3(
            wave * swayAmount * swayWeight,
            0f,
            Mathf.Cos(Time.time * swaySpeed * 0.7f)
                * swayAmount * swayWeight
        );

        transform.position = smoothPosition + swayOffset;

        transform.rotation = Quaternion.Euler(
            90f,
            0f,
            wave * rotationSway * swayWeight
        );

        previousTargetPosition = target.position;
    }
}
