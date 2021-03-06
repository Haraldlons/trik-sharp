﻿module Program

open System
open System.Threading
open System.Collections.Generic
open System.Reactive.Linq
open Trik


type Distance =  Far | Middle | Near

let log s = printfn s

let testMainWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset)
let testSensorsWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset)
let testStripeWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset)
let testMainRunning = ref false
let testSensorsRunning = ref false
let testStripeRunning = ref false

let testPad (model:Model) = 
    let servo1 = model.Servos.[E2]
    let servo2 = model.Servos.[E1]
    let prX1, prY1, prX2, prY2 = ref -1, ref -1, ref -1, ref -1
    let pad = model.Pad
    use dbtn = pad.Buttons.Subscribe (fun num ->  printfn "%A" num)
    use disp = pad.Pads.Subscribe (fun (num, coord) ->  
        match num, coord with
        | (1, Some(x, y) ) -> 
            servo1.SetPower  x
            //if !prX1 = -1 then prX1 := x; prY1 := y 
            //else servo1.SetPower(x - !prX1)
        | (1, None) -> prX1 := -1; prY1 := -1; printfn "None 1";
        | (2, Some(x, _) ) -> servo2.SetPower(-x * 2)
        | (2, None) -> servo2.Zero(); printfn "None 1";
        | _ -> failwith " wrong pad command"
    )

    log "Ready (any key to finish)"
   
let testMain (model:Model) = 
    printfn "testMain (start)"
    testMainRunning := true    
    let rawToDist x = match x with 
                      | _ when x > 80 -> Near
                      | _ when x > 30 -> Middle
                      | _ -> Far
    let distToSpeed x = match x with
                        | Near -> -100
                        | Middle -> 0
                        | Far -> 100

    let rightWheel = model.Motors.[M2]
    let leftWheel = model.Motors.[M1]
    let frontSensor = model.AnalogSensors.[A1].ToObservable().Select(rawToDist)
    let motorActions = frontSensor.Select(fun x ->    //printfn "%A" x;
                                                      distToSpeed x).DistinctUntilChanged()

    use r_disp = motorActions.Subscribe(rightWheel)
    use l_disp = motorActions.Subscribe(leftWheel)

    log "testMain Ready (key to finish)"
    testMainWaitHandle.WaitOne() |> ignore
    testMainRunning := false

let testStripe (model:Model) = 
    printfn "testStripe (start)"
    testStripeRunning := true
    use disp = 
        model.Gyro.ToObservable()
        |> Observable.map 
            (fun coord -> (100 - abs(coord.X/10) - abs(coord.Z/10)
                          , abs(coord.X/10) + abs(coord.Y/10)
                          , abs(coord.Z/10) + abs(coord.Y/10)) )
        |> Observable.subscribe(model.LedStripe.SetPower)
    log "testStripe Ready (key to finish)"
    testStripeWaitHandle.WaitOne() |> ignore
    testStripeRunning := false


let testSensors (model:Model) = 
    printfn "testSensors (start)"
    testSensorsRunning := true  
    let gyro = model.Gyro
    let a = ref 0
    use unsub = 
        gyro.ToObservable() 
        |> Observable.subscribe(fun coord -> 
        a := (!a + 1) % 5; if !a = 0 then printfn "%A" coord.Z else () )

    log "testSensors Ready (key to finish)"
    testSensorsWaitHandle.WaitOne() |> ignore
    testSensorsRunning := false

[<EntryPoint>]
let main _ = 
    log "Started"
    use model = new Model(ServosConfig = dict [| upcast E1, Defaults.Servo4; upcast E2, Defaults.Servo4 |])
    model.Gyro.Start()
    let buttons = model.Buttons
    buttons.Start()
    log "Loaded model"
    let demoList() = 
        printfn "1. testMain\n2. testSensors\n3. testStripe"
    demoList()
    let exit = new EventWaitHandle(false, EventResetMode.AutoReset)
    use disp = 
        buttons.ToObservable() 
        |> Observable.map(fun x -> printfn "recv %A" x;  x)
        |> Observable.subscribe(fun x -> 
            match x.AsTuple with
            | ButtonEventCode.Down, true -> 
                printfn "Exiting (start)"
                exit.Set() |> ignore
            | ButtonEventCode.Right, true -> 
                if not !testMainRunning then 
                    testMainWaitHandle.Reset() |> ignore
                    Async.Start(async { testMain(model); demoList() })
                else 
                    testMainWaitHandle.Set() |> ignore
            | ButtonEventCode.Left, true -> 
                if not !testSensorsRunning then 
                    testSensorsWaitHandle.Reset() |> ignore
                    Async.Start(async { testSensors(model); demoList() })
                else 
                    testSensorsWaitHandle.Set() |> ignore
                    
            | ButtonEventCode.Up, true -> 
                if not !testSensorsRunning then 
                    testSensorsWaitHandle.Reset() |> ignore
                    Async.Start(async { testSensors(model); demoList() } )
                else 
                    testSensorsWaitHandle.Set() |> ignore
            | _ -> () ) 
    exit.WaitOne() |> ignore
    0
