﻿open Elmish
open Xelmish.Model
open Xelmish.Viewables
open Config

type PlayingModel = {
    playerX: int
    invaders: Row []
    invaderDirection: ShuffleState
    projectiles: Projectile list
    lastShuffle: int64
    shuffleInterval: int64
    shuffleMod: int
    freeze: bool
} 
and Row = { kind: InvaderKind; y: int; xs: int [] }
and ShuffleState = Across of row:int * dir:int | Down of row:int * nextDir:int
and Projectile = { x: int; y: int; velocity: int }

let init () = 
    {
        playerX = resWidth / 2 - (playerWidth / 2)
        invaders = 
            [|0..invaderRows-1|]
            |> Array.map (fun row -> 
                let kind = match row with 0 -> smallSize | 1 | 2 -> mediumSize | _ -> largeSize
                {
                    kind = kind
                    y = padding + row * (kind.height + invaderSpacing)
                    xs = 
                        [|0..invadersPerRow-1|] 
                        |> Array.map (fun col -> 
                            padding + col * (largeSize.width + invaderSpacing) + kind.offset) 
                })
        invaderDirection = Across (invaderRows - 1, 1)
        projectiles = []
        lastShuffle = 0L
        shuffleInterval = 500L
        shuffleMod = 0
        freeze = false
    }, Cmd.none

type Message = 
    | MovePlayer of dir: int
    | FireProjectile of Projectile
    | ShuffleInvaders of int64
    | MoveProjectiles
    | PlayerHit
    | Victory

let rec shuffleInvaders time model = 
    let model = { model with shuffleMod = (model.shuffleMod + 1) % 2 }
    
    let (newInvaders, newDirection) = 
        match model.invaderDirection with
        | Across (targetRow, dir) ->
            let newInvaders = 
                model.invaders 
                |> Array.mapi (fun i row -> 
                    if i <> targetRow then row
                    else
                        { row with xs = row.xs |> Array.map (fun x -> x + (invaderShuffleAmount * dir)) })
            if newInvaders.[targetRow].xs |> Array.exists (fun x -> x < padding || x + largeSize.width > (resWidth - padding))
            then model.invaders, Down (model.invaders.Length - 1, dir * -1)
            else newInvaders, Across ((if targetRow = 0 then newInvaders.Length - 1 else targetRow - 1), dir)
        | Down (targetRow, nextDir) ->
            let newInvaders = 
                model.invaders 
                |> Array.mapi (fun i row -> 
                    if i <> targetRow then row
                    else
                        { row with y = row.y + invaderShuffleAmount })
            let nextDirection = 
                if targetRow = 0 then Across (newInvaders.Length - 1, nextDir) 
                else Down (targetRow - 1, nextDir)
            newInvaders, nextDirection

    match model.invaderDirection, newDirection with
    | Across _, Down _ -> shuffleInvaders time { model with invaderDirection = newDirection }
    | _ ->
        let command = 
            let playerRect = rect model.playerX playerY playerWidth playerHeight
            let playerHit =
                newInvaders 
                |> Seq.collect (fun row -> 
                    row.xs |> Seq.map (fun x -> rect x row.y row.kind.width row.kind.height))
                |> Seq.exists (fun (rect: Rectangle) -> rect.Intersects playerRect)
            if playerHit then Cmd.ofMsg PlayerHit else Cmd.none

        { model with 
            invaders = newInvaders
            invaderDirection = newDirection
            lastShuffle = time }, command

let moveProjectiles model =
    let playerProjectile (acc, playerHit, invadersHit) (projectile: Projectile) =
        let next = { projectile with y = projectile.y + projectile.velocity }
        if next.y < 0 then acc, false, invadersHit
        else
            //let projectileRect = rect next.x next.y 1 projectileHeight
            //let hitInvaders = 
            //    model.invaders 
            //    |> List.filter (fun (ix, iy, iw, ih, _) -> 
            //        projectileRect.Intersects(rect ix iy iw ih))
            //if hitInvaders <> [] then
            //    acc, playerHit, hitInvaders @ invadersHit
            //else
            next::acc, playerHit, invadersHit

    let invaderProjectile (acc, playerHit, invadersHit) (projectile: Projectile) =
        let next = { projectile with y = projectile.y + projectile.velocity }
        if next.y > resHeight then acc, playerHit, invadersHit
        else
            let overlapsPlayer = 
                projectile.x >= model.playerX && projectile.x < model.playerX + playerWidth
                && next.y >= playerY
            if overlapsPlayer then acc, true, invadersHit
            else next::acc, playerHit, invadersHit

    let newProjectiles, playerHit, invadersHit =
        (([], false, []), model.projectiles)
        ||> List.fold (fun (acc, playerHit, invadersHit) projectile ->
            if projectile.velocity > 0 then 
                invaderProjectile (acc, playerHit, invadersHit) projectile
            else 
                playerProjectile (acc, playerHit, invadersHit) projectile)
            
    //let newInvaders = List.except invadersHit model.invaders
    let command = 
        if playerHit then Cmd.ofMsg PlayerHit 
        //elif newInvaders = [] then Cmd.ofMsg Victory 
        else Cmd.none
    //{ model with projectiles = newProjectiles; invaders = newInvaders }, command
    { model with projectiles = newProjectiles }, command

let update message model =
    match message with
    | MovePlayer dir ->
        let newPos = min (resWidth - padding - playerWidth) (max padding (model.playerX + dir * playerSpeed))
        { model with playerX = newPos }, Cmd.none
    | FireProjectile projectile ->
        { model with projectiles = projectile::model.projectiles }, Cmd.none
    | ShuffleInvaders time -> shuffleInvaders time model        
    | MoveProjectiles -> moveProjectiles model
    | PlayerHit -> { model with freeze = true }, Cmd.none
    | Victory -> { model with freeze = true }, Cmd.none
    
let sprite (sw, sh, sx, sy) (w, h) (x, y) colour =
    fun loadedAssets _ (spriteBatch: SpriteBatch) ->
        let texture = loadedAssets.textures.["sprites"]
        spriteBatch.Draw (texture, rect x y w h, System.Nullable(rect sx sy sw sh), colour)

let view model dispatch =
    [
        yield! model.invaders 
            |> Array.collect (fun row ->
                let spriteRect = row.kind.animations.[model.shuffleMod]
                row.xs |> Array.map (fun x -> sprite spriteRect (row.kind.width, row.kind.height) (x, row.y) row.kind.colour))

        yield sprite spritemap.["player"] (playerWidth, playerHeight) (model.playerX, playerY) Colour.White

        yield! model.projectiles
            |> List.map (fun projectile ->
                colour Colour.White (1, projectileHeight) (projectile.x, projectile.y))

        if not model.freeze then
            yield fun _ inputs _ -> 
                if inputs.totalGameTime - model.lastShuffle > model.shuffleInterval then
                    dispatch (ShuffleInvaders inputs.totalGameTime)

            yield fun _ _ _ -> dispatch MoveProjectiles

            yield whilekeydown Keys.Left (fun () -> dispatch (MovePlayer -1))
            yield whilekeydown Keys.A (fun () -> dispatch (MovePlayer -1))
            yield whilekeydown Keys.Right (fun () -> dispatch (MovePlayer 1))
            yield whilekeydown Keys.D (fun () -> dispatch (MovePlayer 1))

        yield onkeydown Keys.Space (fun () -> 
            // check that the player hasn't already fired a projectile before adding a new one
            if not (model.projectiles |> List.exists (fun projectile -> projectile.velocity < 0)) then
                {
                    x = model.playerX + playerWidth / 2
                    y = resHeight - (playerHeight + padding) - projectileHeight - 1
                    velocity = -projectileSpeed
                } |> FireProjectile |> dispatch)

        yield onkeydown Keys.Escape exit
    ]

[<EntryPoint>]
let main _ =
    let config: GameConfig = {
        clearColour = Some Colour.Black
        resolution = Windowed (resWidth, resHeight)
        assetsToLoad = [ Texture ("sprites", "./sprites.png") ]
        mouseVisible = false
        showFpsInConsole = true
    }

    Program.mkProgram init update view
    |> Xelmish.Program.runGameLoop config

    0