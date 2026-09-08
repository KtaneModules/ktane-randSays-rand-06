using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;


public class randSaysScript : MonoBehaviour {
	public KMAudio Audio;
    public AudioClip[] SFX;
    //0-9 are 0-9
    //10-19 are 10-19
    //20 is "Stage", 21 is "is"
    //22-29 are 20-90
    //30 is ringing
    public KMBombInfo info;
    public KMBombModule Module;
	public KMSelectable[] Buttons;
	public TextMesh StageText, StageNumberText, IsText;
    public KMBossModule bossModuleHandler;
    public MeshRenderer bgMesh;
    private string[] ignoredModules;

    private int solvables;
    static int ModuleIdCounter;
    int ModuleId;
    private int currentStage;
    private bool canPlay;
    private bool readyToSolve;
    public Color colorB, colorR;
    private int stages;
    private int previousRead = 1, previousAnswer = 1, currentRead = 1, currentAnswer = 1;
    private List<int> reads = new List<int>{1}, answers = new List<int>{1};
    private bool noUpdates;
    private bool failsaved;
    private int halvedFailsafe;

    private int mod(int A, int b) { return b == 0 ? 0 : (A % b + b) % b; }
    
    int getStage(int operationNumber)
    {
        currentRead = currentRead == 0 ? 1 : currentRead;
        previousAnswer = previousAnswer == 0 ? 1 : previousAnswer;
        previousRead = previousRead == 0 ? 1 : previousRead;
        switch (mod(operationNumber,11))
        {
            case 0: return mod((currentRead + mod(2*previousAnswer, currentRead)+mod(4*previousRead, previousAnswer)), 10000);
            case 1: return mod(previousAnswer + mod(3 * previousRead, previousAnswer) + (currentRead * previousAnswer / previousRead),10000);
            case 2: return (int)(Math.Log(previousRead * (1 + previousAnswer * (1 + currentRead))) / Math.Log(2) + Math.Log(currentRead * (1 + previousAnswer * (1 + previousRead))));
            case 3: return mod((int)Math.Abs(currentRead * Math.Sin(previousAnswer + previousRead) / Math.Cos(previousAnswer * previousRead)), 10000);
            case 4: return (int)Math.Abs(previousRead * Math.Cos(Math.Log(currentRead*previousAnswer)/Math.Log(2)));
            case 5: return mod(mod(mod(-currentRead, previousAnswer), mod(previousAnswer,(Math.Abs(previousRead-currentRead)/ 2))) * (currentRead + previousAnswer + previousRead), 10000);
            case 6: return mod((int)(Math.Pow(currentRead*previousAnswer*previousRead,1d/3)+Math.Sqrt((currentRead*currentRead+previousAnswer*previousAnswer+previousRead*previousRead)/9f)+(currentRead+previousAnswer+previousRead)/3f),10000);
            case 7: return mod((int)(3*currentRead*(1-Math.Exp(-previousAnswer/(float)previousRead))),10000);
            case 8: return mod((int)(Math.Pow(Math.Sqrt(currentRead) + Math.Sqrt(previousAnswer) + Math.Sqrt(previousRead), Math.Exp(1))), 10000);
            case 9: return mod(3/(1/currentRead+1/previousAnswer+1/previousRead),10000);
            case 10:
                {
                    int m = mod(previousAnswer * previousRead, 9),
                        n = mod(currentRead * previousRead, 9),
                        p = mod(currentRead * previousAnswer, 9);
                    m = (m == 0 ? 9 : m)+1;
                    n = (n == 0 ? 9 : n)+1;
                    p = (p == 0 ? 9 : p)+1;
                    return mod((int)(currentRead*p*Math.Log(n)/Math.Log(m)),10000);
                }
            default: return 0;
        }
    }

    void changeColor(Color color)
    {
        bgMesh.material.color = color;
    }

    void failsafeMode(string code)
    {
        // 1** - stage **,
        // 2*2 - stage *0,
        // 33* - stage 0*,
        // 444 - stage 00,
        // 555 - exit,
        // 888 - finalAns on halfway
        StageNumberText.text = "";
        if (code == "555")
        {
            changeColor(colorB);
            failsaved = false;
            return;
        }
        if (code == "888")
        {
            if ((reads.Count - 1 - halvedFailsafe) / 2 == 0) return;
            halvedFailsafe += (reads.Count - 1 - halvedFailsafe)/2;
            playStage(halvedFailsafe, answers[halvedFailsafe]);
            return;
        }
        
        int stageToRecover = -1;
        if (code[0] == '1') stageToRecover = code[1] * 10 + code[2] - 528;
        if (code[0] == '2' && code[2] == '2') stageToRecover = code[1] * 10 - 480;
        if (code[0] == '3' && code[1]=='3')  stageToRecover = code[2] - 48;
        if (code == "444") stageToRecover = 0;
        if (stageToRecover > reads.Count){
            changeColor(colorB);
            failsaved = false;
            return;
        }
        playStage(stageToRecover, reads[stageToRecover]);
    }
    
    private static bool busyCoroutine;
    IEnumerator StartAudioSequence(int stageNum, int numberToRead, float timeout = 0.0f)
    {
        if (busyCoroutine) yield break;
        busyCoroutine = true;
        StageText.text = "Stage";
        Audio.HandlePlaySoundAtTransform(SFX[20].name, transform);
        yield return new WaitForSeconds(SFX[20].length + timeout);
        StageNumberText.text = stageNum.ToString("D2");
        if ((stageNum / 10 >= 2 ? stageNum / 10 : -1) != -1)
        {
            Audio.HandlePlaySoundAtTransform(SFX[stageNum / 10 >= 2 ? stageNum / 10 : -1].name, transform);
            yield return new WaitForSeconds(SFX[stageNum / 10 >= 2 ? stageNum / 10 : -1].length + timeout);
        }
        if((stageNum < 20 ? stageNum : stageNum % 10 == 0 ? -1 : stageNum % 10)!=-1)
        {
            Audio.HandlePlaySoundAtTransform(SFX[stageNum < 20 ? stageNum : stageNum % 10 == 0 ? -1 : stageNum % 10].name, transform);
            yield return new WaitForSeconds(SFX[stageNum < 20 ? stageNum : stageNum % 10 == 0 ? -1 : stageNum % 10].length + timeout);
        }
        IsText.text = "is ";
        Audio.HandlePlaySoundAtTransform(SFX[21].name, transform);
        yield return new WaitForSeconds(SFX[21].length + timeout);
        IsText.text += "?";
        Audio.HandlePlaySoundAtTransform(SFX[numberToRead / 1000].name, transform);
        yield return new WaitForSeconds(SFX[numberToRead / 1000].length + timeout);
        IsText.text += "?";
        Audio.HandlePlaySoundAtTransform( SFX[numberToRead / 100 % 10].name, transform);
        yield return new WaitForSeconds( SFX[numberToRead / 100 % 10].length + timeout);
        IsText.text += "?";
        Audio.HandlePlaySoundAtTransform(SFX[numberToRead / 10 % 10].name, transform);
        yield return new WaitForSeconds(SFX[numberToRead / 10 % 10].length + timeout);
        IsText.text += "?";
        Audio.HandlePlaySoundAtTransform(SFX[numberToRead % 10].name, transform);
        yield return new WaitForSeconds(SFX[numberToRead % 10].length + timeout);
        StageText.text = "";
        IsText.text = "";
        if (failsaved)
        {
            StageNumberText.text = "";
            failsaved = false;
            changeColor(colorB);
        }
        busyCoroutine = false;
    }

    void playStage(int stageNum, int numberToRead)
    {
        StartCoroutine(StartAudioSequence(stageNum, numberToRead));
    }

    int generateSol(int finalAnswer)
    {
        if (finalAnswer % 10 == 0) finalAnswer += 375;
        if (finalAnswer /10 % 10 == 0) finalAnswer += 24;
        if (finalAnswer /100 % 10 == 0) finalAnswer += 713;
        if (finalAnswer /1000 % 10 == 0) finalAnswer += 4016;
        finalAnswer %= 10000;
        if (finalAnswer % 10 == 0) finalAnswer += 1;
        if (finalAnswer /10 % 10 == 0) finalAnswer += 10;
        if (finalAnswer /100 % 10 == 0) finalAnswer += 100;
        if (finalAnswer /1000 % 10 == 0) finalAnswer += 1000;
        return finalAnswer;
    }

    void HandlePress(int num)
    {
        if (readyToSolve)
        {
            StageNumberText.text += (char)('0' + num);
            if (failsaved)
            {
                if (StageNumberText.text.Length == 3)
                {
                    failsafeMode(StageNumberText.text);
                }
            }
            else
            if (StageNumberText.text.Length == 4)
            {
                if (StageNumberText.text == generateSol(answers.Last()).ToString())
                {
                    Module.HandlePass();
                }
                else
                {
                    Module.HandleStrike();
                    failsaved = true;
                    changeColor(colorR);
                    StageNumberText.text = "";
                }
            }
        }
        else
        {
            canPlay = false;
            playStage(currentStage, currentRead);
        }
    }

    void NewStage(int stage)
    {
        currentStage = stage;
        StageNumberText.text = currentStage.ToString("D2");
        
        if (canPlay) Module.HandleStrike();
        else canPlay = true;
        previousRead = currentRead;
        previousAnswer = currentAnswer;
        currentRead = UnityEngine.Random.Range(1, 10000);
        char sn = info.GetSerialNumber()[mod(previousAnswer, 6)];
        int s = sn > '9' ? sn - 'A' + 10 : sn - '0';
        currentAnswer = getStage(mod(s+mod(currentRead,19),11));
        reads.Add(currentRead);
        answers.Add(currentAnswer);
        print($"Stage {stage} -> {currentRead}, answer is {currentAnswer}");
    }

    void Start ()
    {
        Module.OnActivate += delegate
        {
            Audio.PlaySoundAtTransform(SFX[30].name, transform);
        };
        StageNumberText.text = "00";
        StageText.text = "";
        IsText.text = "";
        ModuleId = ModuleIdCounter++;
        currentStage = 0;
        readyToSolve = false;
        changeColor(colorB);
        if (ignoredModules == null) {
            ignoredModules = bossModuleHandler.GetIgnoredModules("Confirmation Codes", new[] {
        "+",
        "14",
        "42",
        "501",
        "Access Codes",
        "Amnesia",
        "A>N<D",
        "Bamboozling Time Keeper",
        "Black Arrows",
        "Brainf---",
        "Busy Beaver",
        "Button Messer",
        "Confirmation Codes",
        "Cookie Jars",
        "Cube Synchronization",
        "Divided Squares",
        "Don't Touch Anything",
        "Encrypted Hangman",
        "Encryption Bingo",
        "Floor Lights",
        "Forget Any Color",
        "Forget Enigma",
        "Forget Everything",
        "Forget Infinity",
        "Forget It Not",
        "Forget Me Later",
        "Forget Me Not",
        "Forget Perspective",
        "Forget The Colors",
        "Forget Them All",
        "Forget This",
        "Forget Us Not",
        "Four-Card Monte",
        "The Heart",
        "Hogwarts",
        "Iconic",
        "Keypad Directionality",
        "The Klaxon",
        "Kugelblitz",
        "Multitask",
        "Mystery Module",
        "OmegaForget",
        "OmegaDestroyer",
        "Organization",
        "Purgatory",
        "RPS Judging",
        "Security Council",
        "Shoddy Chess",
        "Simon",
        "Simon Forgets",
        "Simon's Stages",
        "Souvenir",
        "SuperBoss",
        "The Swan",
        "Tallordered Keys",
        "The Time Keeper",
        "Timing is Everything",
        "The Troll",
        "Turn The Key",
        "The Twin",
        "Übermodule",
        "Ultimate Custom Night",
        "The Very Annoying Button",
        "Whiteout",
        });
        }
        solvables = info.GetSolvableModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
        print($"Found {solvables} solvables.");
        if (!(solvables > 0)) Module.HandlePass();
        stages = solvables / 3;
        stages = stages > 99 ? 99 : stages;
        
        for (int i = 0; i < 9; i++)
        {
            int i1 = i;
            Buttons[i1].OnInteract += delegate{HandlePress(i1+1); return false;};
        }
    }

    void Update()
    {
        if (noUpdates) return;
        int solved = info.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
        if (solved == solvables)
        {
            if (busyCoroutine) return;
            noUpdates = true;
            StageNumberText.text = "";
            readyToSolve = true;
            print($"Final answer: {generateSol(answers.Last())}");
            return;
        }
        if (solved / 3 != currentStage) NewStage(solved / 3);
    }

}
