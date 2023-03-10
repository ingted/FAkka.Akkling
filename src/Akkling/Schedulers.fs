//-----------------------------------------------------------------------
// <copyright file="Schedulers.fs" company="Akka.NET Project">
//     Copyright (C) 2009-2015 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2015 Akka.NET project <https://github.com/akkadotnet/akka.net>
//     Copyright (C) 2015-2020 Bartosz Sypytkowski <gttps://github.com/Horusiath>
// </copyright>
//-----------------------------------------------------------------------

[<AutoOpen>]
module Akkling.Schedulers

open Akka.Actor
open System

type IActionScheduler with
    
    /// <summary>
    /// Schedules a function to be invoked repeatedly in the provided time intervals. 
    /// </summary>
    /// <param name="after">Initial delay to first function call.</param>
    /// <param name="every">Interval.</param>
    /// <param name="fn">Function called by the scheduler.</param>
    /// <param name="cancelable">Optional cancelation token</param>
    member this.ScheduleRepeatedly(after : TimeSpan, every : TimeSpan, fn : unit -> unit, ?cancelable : ICancelable) : unit = 
        let action = Action fn
        match cancelable with
        | Some c -> this.ScheduleRepeatedly(after, every, action, c)
        | None -> this.ScheduleRepeatedly(after, every, action)
    
    /// <summary>
    /// Schedules a single function call using specified sheduler.
    /// </summary>
    /// <param name="after">Delay before calling the function.</param>
    /// <param name="fn">Function called by the scheduler.</param>
    /// <param name="cancelable">Optional cancelation token</param>
    member this.ScheduleOnce(after : TimeSpan, fn : unit -> unit, ?cancelable : ICancelable) : unit = 
        let action = Action fn
        match cancelable with
        | Some c -> this.ScheduleOnce(after, action, c)
        | None -> this.ScheduleOnce(after, action)

type ITellScheduler with
    
    /// <summary>
    /// Schedules a <paramref name="message"/> to be sent to the provided <paramref name="receiver"/> in specified time intervals.
    /// </summary>
    /// <param name="after">Initial delay to first function call.</param>
    /// <param name="every">Interval.</param>
    /// <param name="message">Message to be sent to the receiver by the scheduler.</param>
    /// <param name="receiver">Message receiver.</param>
    member this.ScheduleTellRepeatedly(after : TimeSpan, every : TimeSpan, receiver : ICanTell<'Message>, message : 'Message) : unit = 
        this.ScheduleTellRepeatedly(after, every, receiver.Underlying, message, ActorRefs.NoSender)
    
    /// <summary>
    /// Schedules a single <paramref name="message"/> send to the provided <paramref name="receiver"/>.
    /// </summary>
    /// <param name="after">Delay before sending a message.</param>
    /// <param name="message">Message to be sent to the receiver by the scheduler.</param>
    /// <param name="receiver">Message receiver.</param>
    member this.ScheduleTellOnce(after : TimeSpan, receiver : ICanTell<'Message>, message : 'Message) : unit = 
        this.ScheduleTellOnce(after, receiver.Underlying, message, ActorRefs.NoSender)
