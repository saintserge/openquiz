module rec Main

open Elmish
open Fable.React
open Fable.FontAwesome
open Fable.React.Props
open Elmish.React

open Shared
open Common


type Msg =
    | ToggleBurger
    | Logout
    | Public of MainPub.Msg
    | Prod of MainProd.Msg
    | SwithToPublic
    | SwithToProd
    | BecomeProducer
    | AcceptTermsOfUse
    | CancelTermsOfUse
    | BecomeProducerResp of RESP<unit>
    | Exn of exn
    | DeleteError of string


type Model = {
    IsBurgerOpen : bool
    IsTermsOfUseOpen : bool
    Errors : Map<string, string>
    Area : Area
}

type Area =
    | Public of MainPub.Model
    | Prod of MainProd.Model

let addError txt model =
    {model with Errors = model.Errors.Add(System.Guid.NewGuid().ToString(),txt)}

let delError id model =
    {model with Errors = model.Errors.Remove id}

let init api user : Model*Cmd<Msg> =
    let subModel,subCmd = MainPub.init api user
    {IsBurgerOpen = false; IsTermsOfUseOpen = false; Errors = Map.empty; Area = Public subModel}, Cmd.map Msg.Public subCmd

let update (api:IMainApi) user (msg : Msg) (cm : Model) : Model * Cmd<Msg> =
    match msg, cm.Area with
    | ToggleBurger, _ -> {cm with IsBurgerOpen = not cm.IsBurgerOpen} |> noCmd
    | BecomeProducer, _ -> {cm with IsTermsOfUseOpen = true} |> noCmd
    | CancelTermsOfUse, _ -> {cm with IsTermsOfUseOpen = false} |> noCmd
    | AcceptTermsOfUse, _ -> {cm with IsTermsOfUseOpen = false} |> apiCmd api.becomeProducer () BecomeProducerResp Exn
    | BecomeProducerResp {Value = Ok _}, _ -> cm, Cmd.OfFunc.result SwithToProd
    | DeleteError id, _ -> cm |> delError id |> noCmd
    | SwithToProd, _ ->
        let subModel,subCmd = MainProd.init api user
        {cm with Area = Prod subModel}, Cmd.map Msg.Prod subCmd
    | SwithToPublic, _ ->
        let subModel,subCmd = MainPub.init api user
        {cm with Area = Public subModel}, Cmd.map Msg.Public subCmd
    | Logout, _ ->  Infra.clearUserAndRedirect "/"; cm|> noCmd
    | Msg.Public subMsg, Public subModel ->
        let subModel,subCmd = MainPub.update api user subMsg subModel
        {cm with Area = Public subModel}, Cmd.map Msg.Public subCmd
    | Msg.Prod subMsg, Prod subModel ->
        let subModel,subCmd = MainProd.update api user subMsg subModel
        {cm with Area = Prod subModel}, Cmd.map Msg.Prod subCmd
    | Err txt, _ -> cm |> addError txt |> noCmd
    | Exn ex, _ -> cm |> addError ex.Message |> noCmd
    | _ -> cm |> noCmd

let view (dispatch : Msg -> unit) (user:MainUser) (model : Model) =

    let isProd = match model.Area with Public _  -> false | Prod _ -> true

    section [classList ["hero", true; "is-shadowless", true; "is-fullheight", true; "is-dark", isProd]] [
        div [Class "hero-head"] [
            div [Class "container"][
                nav [Class "navbar is-transparent is-spaced"; Role "navigation"; AriaLabel "dropdown navigation"] [
                    div [Class "navbar-brand"] [
                        a [Href "/app/"; Class "navbar-item navbar-logo"; Style [MarginRight "auto"]] [
                            figure [Class "image is-64x64"][
                                img [Src "/logo.png"; Alt "logo"; Style [Height "64px"; Width "64px"; MaxHeight "64px"]]
                            ]
                        ]

                        a [Class "navbar-item is-paddingleft is-hidden-desktop"][str user.Name]

                        a [Role "button"; Class "navbar-burger"; Style [MarginLeft "0"]; AriaLabel "menu"; AriaExpanded false; OnClick (fun _ -> dispatch ToggleBurger)][
                            span [AriaHidden true][]
                            span [AriaHidden true][]
                            span [AriaHidden true][]
                        ]
                    ]
                    div [classList ["navbar-menu", true; "is-active", model.IsBurgerOpen]] [
                        div [Class "navbar-start"] [
                            a [Class "navbar-item is-hidden-desktop"; OnClick (fun _ -> dispatch Logout)] [str "Logout"]
                        ]
                        div [Class "navbar-end"] [
                            div [Class "navbar-item has-dropdown is-hoverable is-hidden-touch"] [
                                a [Class "navbar-link"] [
                                    figure [Class "image"][
                                        img [Class "is-rounded"; Style [Height "50px"; Width "50px"; MaxHeight "50px"]; Src user.PictureUrl]
                                    ]
                                    span [Style [MarginLeft "5px"]][str user.Name]
                                ]
                                div [Class "navbar-dropdown is-boxed"][
                                    a [Class "navbar-item"; OnClick (fun _ -> dispatch Logout)] [str "Logout"]
                                ]
                            ]
                        ]
                    ]
                    if not user.IsPrivate then
                        match user.IsProducer, model.Area with
                        | true, Public _ ->
                            a [Class "navbar-item has-text-danger"; OnClick (fun _ -> dispatch SwithToProd)][
                                Fa.i [Fa.Regular.HandPointer][str " to production area"]
                            ]
                        | true, Prod _ ->
                            a [Class "navbar-item has-text-danger"; OnClick (fun _ -> dispatch SwithToPublic)][
                                Fa.i [Fa.Solid.HandPointer][str " to public area"]
                            ]
                        | false, Public _ ->
                            a [Class "navbar-item has-text-danger"; OnClick (fun _ -> dispatch BecomeProducer)][
                                Fa.i [Fa.Regular.HandPointer][str " become a quiz maker!"]
                            ]
                        | _ -> ()
                ]
            ]
        ]
        div [Class "hero-body"] [
            div [Class "container"; Style [MarginBottom "auto"]] [

                for error in model.Errors do
                    div [Class "notification is-danger"][
                        button [Class "delete"; OnClick (fun _ -> dispatch (DeleteError error.Key))][]
                        str error.Value
                    ]

                match model.Area with
                | Public subModel -> MainPub.view (Msg.Public >> dispatch) user subModel
                | Prod subModel -> MainProd.view (Msg.Prod >> dispatch) user subModel
            ]
        ]

        MainTemplates.footer

        if model.IsTermsOfUseOpen then
            MainTemplates.termsOfUse dispatch AcceptTermsOfUse CancelTermsOfUse
    ]