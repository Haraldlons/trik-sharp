﻿module Program

open System
open System.Collections.Generic
open System.Reactive.Linq
open Trik
open System.Threading

type Distance =  Far | Middle | Near

let log s = printfn s

let button = new Button("/dev/input/event0")   


let testMainWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset)
let testSensorsWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset)
let testMainRunning = ref false
let testSensorsRunning = ref false

let testPad (model:Model) = 
    let servo1 = model.Servo.["JE2"]
    let servo2 = model.Servo.["JE1"]
    let prX1, prY1, prX2, prY2 = ref -1, ref -1, ref -1, ref -1
    let pad = model.Pad
    use dbtn = pad.Buttons.Subscribe (fun num ->  printfn "%A" num)
    use disp = pad.Pads.Subscribe ( fun (num, coord) ->  
        //printfn "%A %A" num coord 
        match num, coord with
        | (1, Some(x, y) ) -> 
            servo1.SetPower(x)
            //if !prX1 = -1 then prX1 := x; prY1 := y 
            //else servo1.SetPower(x - !prX1)
        | (1, None) -> prX1 := -1; prY1 := -1; printfn "None 1";
        | (2, Some(x, _) ) -> servo2.SetPower(- x * 2)
        | (2, None) -> servo2.Zero(); printfn "None 1";
        | (_, _) -> () 
    )

    log "Ready (any key to finish)"
   
let testMain (model:Model) = 
    testMainRunning := true    
    let rawToDist x = match x with 
                      | _ when x > 80 -> Near
                      | _ when x > 30 -> Middle
                      | _ -> Far
    let distToSpeed x = match x with
                        | Near -> -100
                        | Middle -> 0
                        | Far -> 100

    let rightWheel = model.Motor.["JM2"]
    let leftWheel = model.Motor.["JM1"]
    let frontSensor = model.AnalogSensor.["JA1"].ToObservable().Select(rawToDist)
    let motorActions = frontSensor.Select(fun x ->    printfn "%A" x;
                                                      distToSpeed x).DistinctUntilChanged()

    use r_disp = motorActions.Subscribe(rightWheel)
    use l_disp = motorActions.Subscribe(leftWheel)

    log "testMain Ready (key to finish)"
    testMainWaitHandle.WaitOne() |> ignore
    testMainRunning := false

let testSensors (model:Model) = 
    testSensorsRunning := true  
    let gyro = model.Gyro
    let a = ref 0
    use unsub = 
        gyro.ToObservable() 
        |> Observable.subscribe(fun (x: int array) -> 
        a := (!a + 1) % 5; if !a = 0 then printfn "%A" x.[2] else () )

    log "testSensors Ready (key to finish)"
    testSensorsWaitHandle.WaitOne() |> ignore
    testSensorsRunning := false

[<EntryPoint>]
let main _ = 
    log "Started"
    Helpers.I2C.init "/dev/i2c-2" 0x48 1
    use model = new Model(ServoConfig = [| 
                              ("JE1", "/sys/class/pwm/ehrpwm.1:1", 
                                { stop = 0; zero = 1310000; min = 1200000; max = 1420000; period = 20000000 } )
                              ("JE2", "/sys/class/pwm/ehrpwm.1:0", 
                                { stop = 0; zero = 1550000; min =  800000; max = 2250000; period = 20000000 } )
                             |])
    log "Loaded model"
    //testSensors model
    //testMain model
    let exit = new EventWaitHandle(false, EventResetMode.AutoReset)
    use disp = 
        button.ToObservable() 
        |> Observable.map(fun x -> printfn "recv %A" x;  x)
        |> Observable.subscribe(function 
            | Button_Event_Code.Down, true -> exit.Set() |> ignore
            | Button_Event_Code.Right, true -> 
                if not !testMainRunning then 
                    testMainWaitHandle.Reset() |> ignore
                    testMain(model)
                else 
                    testMainWaitHandle.Set() |> ignore
            | Button_Event_Code.Up, true -> 
                if not !testSensorsRunning then 
                    testSensorsWaitHandle.Reset() |> ignore
                    testSensors(model)
                else 
                    testSensorsWaitHandle.Set() |> ignore
            | _ -> () ) 
    exit.WaitOne() |> ignore
    0
