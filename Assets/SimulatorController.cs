using UnityEngine;
using UnityEngine.Splines;

public class SimulatorController : MonoBehaviour
{
    public enum Step
    {
        Spline1A,
        Spline1B,
        Spline2A,
        Spline2B,
        Spline3AB
    }

    [Header("Current Step")]
    [SerializeField] private Step currentStep = Step.Spline1A;

    [Header("Objects")]
    public VehicleBehavior harvester;

    [Header("Object Animators")]
    public SplineAnimate animatorA;
    public SplineAnimate animatorB;

    [Header("Object A Paths")]
    public SplineContainer pathA_1;
    public SplineContainer pathA_2;
    public SplineContainer pathA_3;

    [Header("Object B Paths")]
    public SplineContainer pathB_1;
    public SplineContainer pathB_2;
    public SplineContainer pathB_3;

    private bool aFinishedStep3 = false;
    private bool bFinishedStep3 = false;

    void Start()
    {
        animatorA.Completed += OnAnimatorACompleted;
        animatorB.Completed += OnAnimatorBCompleted;

        currentStep = Step.Spline1A;
        RunStep(pathA_1, animatorA);
    }

    private void OnAnimatorACompleted()
    {
        switch (currentStep)
        {
            case Step.Spline1A:
                currentStep = Step.Spline1B;
                RunStep(pathB_1, animatorB);
                break;
            case Step.Spline2A:
                currentStep = Step.Spline2B;
                RunStep(pathB_2, animatorB);
                break;
            case Step.Spline3AB:
                aFinishedStep3 = true;
                CheckSequenceCompletion();
                break;
        }
    }

    private void OnAnimatorBCompleted()
    {
        switch (currentStep)
        {
            case Step.Spline1B:
                currentStep = Step.Spline2A;
                RunStep(pathA_2, animatorA);
                harvester.load = 0; 
                print("Load Reset");
                harvester.loadText.text = harvester.load.ToString() + "/" + harvester.capacity.ToString();
                break;
            case Step.Spline2B:
                harvester.load = 0; 
                print("Load Reset");
                harvester.loadText.text = harvester.load.ToString() + "/" + harvester.capacity.ToString();
                currentStep = Step.Spline3AB;
                RunStep(pathA_3, animatorA);
                RunStep(pathB_3, animatorB);
                break;
            case Step.Spline3AB:
                bFinishedStep3 = true;
                CheckSequenceCompletion();
                break;
        }
    }

    private void RunStep(SplineContainer path, SplineAnimate animator)
    {
        animator.Container = path;
        animator.Restart(true);
    }

    private void CheckSequenceCompletion()
    {
        if (aFinishedStep3 && bFinishedStep3)
        {
            Debug.Log("Both animators have completed Step 3. Sequence finished.");
        }
    }
}
