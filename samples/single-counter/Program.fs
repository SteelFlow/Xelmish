﻿open Elmish
open Xelmish.Model
open Xelmish.Viewables

type Model =
  { Count: int
    StepSize: int }

let init () =
  { Count = 0
    StepSize = 1 }

type Msg =
  | Increment
  | Decrement
  | SetStepSize of int
  | Reset

let update msg m =
  match msg with
  | Increment -> { m with Count = m.Count + m.StepSize }
  | Decrement -> { m with Count = m.Count - m.StepSize }
  | SetStepSize x -> { m with StepSize = x }
  | Reset -> init ()

let view model dispatch =
    let text = text "connection" 16. (rgb 255uy 255uy 255uy)
    let button s event size pos = [
        colour (rgb 0uy 0uy 255uy) size pos
        text s pos
        clickable event size pos
    ]
    [
        yield text (sprintf "Counter value: %i" model.Count) (100, 50)

        yield! 
            stack 200 ([
                (fun p ->
                    row 600 [
                        button "- counter" (fun () -> dispatch Decrement)
                        button "+ counter" (fun () -> dispatch Increment)
                    ] p)
                fun p -> [text (sprintf "Step size: %i" model.StepSize) p]
                (fun p ->
                    row 600 [
                        button "- step size" (fun () -> dispatch (SetStepSize (model.StepSize - 1)))
                        button "+ step size" (fun () -> dispatch (SetStepSize (model.StepSize + 1)))
                    ] p)
                fun p -> [button (sprintf "Step size: %i" model.StepSize) (fun () -> dispatch Reset) p]
            ] |> List.collect id) (100, 100)
    ]

[<EntryPoint>]
let main _ =
    let config = {
        resolution = Windowed (800, 600)
        clearColour = Some (rgb 255uy 0uy 0uy)
        mouseVisible = true
        assetsToLoad = [
            Font ("connection", "./connection")
        ]
    }

    Program.mkSimple init update view
    |> Program.withConsoleTrace
    |> Xelmish.Program.runGameLoop config
    0