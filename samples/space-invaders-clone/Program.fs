﻿open Elmish
open Xelmish.Model
open Xelmish.Viewables

let resWidth = 800
let resHeight = 600
let playerSpeed = 5
let padding = 30
let invaderDim = 40
let invaderSpacing = 20
let invaderShuffleAmount = 20
let playerDim = 40
let projectileHeight = 10
let projectileSpeed = 10

type Model = {
    playerX: int
    invaders: (int * int) list
    invaderDirection: int
    bunkers: (int * int) list
    projectiles: (int * int * int) list
    lastShuffle: int64
    shuffleInterval: int64
}

let init () = 
    {
        playerX = resWidth / 2 - (playerDim / 2)
        invaders = 
            [0..8*5-1] 
            |> List.map (fun i ->
                let y = padding + (i / 8) * (invaderDim + invaderSpacing)
                let x = padding + (i % 8) * (invaderDim + invaderSpacing)
                x, y)
        invaderDirection = 1
        bunkers = []
        projectiles = []
        lastShuffle = 0L
        shuffleInterval = 500L
    }, Cmd.none

type Message = 
    | MovePlayer of dir: int
    | FireProjectile of x: int * y: int * velocity: int
    | ShuffleInvaders of int64
    | MoveProjectiles

let update message model =
    match message with
    | MovePlayer dir ->
        let newPos = min (resWidth - padding - playerDim) (max padding (model.playerX + dir * playerSpeed))
        { model with playerX = newPos }, Cmd.none
    | FireProjectile (x, y, v) ->
        { model with projectiles = (x, y, v)::model.projectiles }, Cmd.none
    | ShuffleInvaders time ->
        let (newInvaders, valid) = 
            (([], true), model.invaders)
            ||> List.fold (fun (acc, valid) (x, y) ->
                if not valid then (acc, valid)
                else
                    let nx = x + invaderShuffleAmount * model.invaderDirection
                    if nx < padding || nx + invaderDim > (resWidth - padding) then acc, false
                    else (nx, y)::acc, true)
        if not valid then
            // drop invaders
            // check for player impact
            { model with invaderDirection = model.invaderDirection * -1; lastShuffle = time }, 
            Cmd.ofMsg (ShuffleInvaders time)
        else
            { model with invaders = newInvaders; lastShuffle = time }, Cmd.none
    | MoveProjectiles ->
        let newProjectiles =
            ([], model.projectiles)
            ||> List.fold (fun acc (x, y, v) ->
                let newY = y + v
                if newY > resHeight || newY < -projectileHeight then acc
                else (x, newY, v)::acc)
        // check for player inpact
        // check for invader inpact
        { model with projectiles = newProjectiles }, Cmd.none

let view model dispatch =
    [
        yield! 
            model.invaders 
            |> List.map (fun invaderPos ->
                colour Colour.Green (invaderDim, invaderDim) invaderPos)

        yield colour Colour.Red (playerDim, playerDim) (model.playerX, resHeight - (playerDim + padding))

        yield!
            model.projectiles
            |> List.map (fun (x, y, _) ->
                colour Colour.White (1, projectileHeight) (x, y))

        yield 
            fun _ inputs _ -> 
                if inputs.totalGameTime - model.lastShuffle > model.shuffleInterval then
                    dispatch (ShuffleInvaders inputs.totalGameTime)

        yield fun _ _ _ -> dispatch MoveProjectiles

        yield whilekeydown Keys.Left (fun () -> dispatch (MovePlayer -1))
        yield whilekeydown Keys.Right (fun () -> dispatch (MovePlayer 1))

        yield onkeydown Keys.Space (fun () -> 
            let x = model.playerX + playerDim / 2
            let y = resHeight - (playerDim + padding) - projectileHeight - 1
            dispatch (FireProjectile (x, y, -projectileSpeed)))

        yield onkeydown Keys.Escape exit
    ]

[<EntryPoint>]
let main _ =
    let config: GameConfig = {
        clearColour = Some Colour.Black
        resolution = Windowed (resWidth, resHeight)
        assetsToLoad = []
        mouseVisible = false
    }

    Program.mkProgram init update view
    |> Xelmish.Program.runGameLoop config
    0
