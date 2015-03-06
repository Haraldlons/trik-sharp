﻿namespace Trik
open System
open System.Collections.Generic
open Trik.Collections
open Trik.Sensors

type Model () as model = 

    static do Helpers.I2C.Init "/dev/i2c-2" 0x48 1
              IO.File.WriteAllText("/sys/class/gpio/gpio62/value", "1")
              Helpers.SendToShell (String.Concat(List.map (sprintf "i2cset -y 2 0x48 %d 0x1000 w; ") [0x10 .. 0x13]))
                                                
    static let resources = new ResizeArray<_>()

    let lazyPropertyInit config ctor = lazy(config |> Array.map (fun (key, params') -> (key, ctor params')) |> dict)

    let mutable gyro =         lazy new Gyroscope(-32767, 32767, "/dev/input/by-path/platform-spi_davinci.1-event")
    let mutable accel =        lazy new Accelerometer(-32767, 32767, "/dev/input/event1")
    let mutable led =          lazy new Led()
    let mutable pad =          lazy new PadServer(model.PadConfigPort)
    let mutable ledStripe =    lazy new LedStripe(model.LedStripeConfig)
    let mutable lineSensor =   lazy new LineSensor(model.LineSensorConfig)
    let mutable objectSensor = lazy new ObjectSensor(model.ObjectSensorConfig)  
    let mutable mxnSensor =    lazy new MXNSensor(model.ObjectSensorConfig)
    let mutable servo =        lazyPropertyInit model.ServoConfig        (fun x -> new ServoMotor(x))
    let mutable motor =        lazyPropertyInit model.MotorConfig        (fun (cnum:int) -> new PowerMotor(cnum))
    let mutable encoder =      lazyPropertyInit model.EncoderConfig      (fun cnum -> new Encoder(cnum))
    let mutable analogSensor = lazyPropertyInit model.AnalogSensorConfig (fun (cnum:int) -> new AnalogSensor(cnum))
    
    let mutable isDisposed = false
    
    do AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> (model :> IDisposable).Dispose())

    member val PadConfigPort = 4444 with get, set
    member val ServoConfig = 
        [| 
          ("E1", ("/sys/class/pwm/ehrpwm.1:1", Defaults.Servo3 ))
          ("E2", ("/sys/class/pwm/ehrpwm.1:0", Defaults.Servo3 ))
          ("E3", ("/sys/class/pwm/ehrpwm.0:1", Defaults.Servo3 ))
          ("C1", ("/sys/class/pwm/ecap.0", Defaults.Servo4 ))
          ("C2", ("/sys/class/pwm/ecap.1", Defaults.Servo4 ))
          ("C3", ("/sys/class/pwm/ecap.2", Defaults.Servo4 ))
         |] with get, set
    member val EncoderConfig =
        [| 
          ("B1", 0x30)
          ("B2", 0x31)
          ("B3", 0x32)
          ("B4", 0x33)
         |] with get, set
    member val MotorConfig = 
        [| 
          ("M1", 0x14)
          ("M2", 0x15)
          ("M3", 0x17)
          ("M4", 0x16)
         |] with get, set
    member val LedStripeConfig = Defaults.LedSripe with get, set
    member val AnalogSensorConfig = 
        [| 
          ("A1", 0x25)
          ("A2", 0x24)
          ("A3", 0x23)
          ("A4", 0x22)
          ("A5", 0x21)
          ("A6", 0x20)
        |] with get, set
    member val LineSensorConfig = Ports.VideoSource.VP2
         with get, set
    member val ObjectSensorConfig = Ports.VideoSource.VP2
        with get, set
    member val MXNSensorConfig = Ports.VideoSource.VP2
        with get, set

    member x.Motor with get() = motor.Force()
         
    member x.Servo with get() = servo.Force()
    
    member x.AnalogSensor with get() = analogSensor.Force()
    
    member x.Encoder with get() = encoder.Force()

    member x.Gyro with get() = gyro.Force()

    member x.Accel with get() = accel.Force()
        
    member x.Led with get() = led.Force()

    member x.LedStripe with get() = ledStripe.Force()
    
    member x.Pad with get() = pad.Force()

    member self.LineSensor with get() = lineSensor.Force()

    member self.ObjectSensor with get() = objectSensor.Force()

    member self.MXNSensor with get() = mxnSensor.Force()

    static member RegisterResource(d: IDisposable) = lock resources <| fun () -> resources.Add(d)

    interface IDisposable with
        member self.Dispose() = 
            lock self 
            <| fun () -> 
                   if not isDisposed then
                        resources.ForEach(fun x -> x.Dispose()) 
                        let inline dispose (device: Lazy<'T> when 'T :> IDisposable) = 
                             if device.IsValueCreated then device.Force().Dispose()
                        let inline disposeMap (devices: Lazy<IDictionary<'TKey, 'T>> when 'T :> IDisposable) = 
                            if devices.IsValueCreated then devices.Force().Values |> (Seq.iter (fun x -> x.Dispose()))
                        dispose lineSensor
                        dispose objectSensor
                        dispose mxnSensor
                        dispose gyro
                        dispose accel
                        dispose led
                        dispose pad
                        dispose ledStripe
                        disposeMap motor
                        disposeMap servo
                        disposeMap analogSensor
                        isDisposed <- true
            