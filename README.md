# RailCAD.NET

## About
C# .NET module for the RailCAD application which is currently written in AutoLISP. Build with `.NET 8.0` or `.NET Framework 4.8`.

## Supported CADs
- AutoCAD
- BricsCAD
- GstarCAD
- ZWCAD

## Used third-party libraries

| Library | Licence |
| --- | --- |
| [delaunator-sharp](https://github.com/nol1fe/delaunator-sharp) | MIT

# Compilation

- conditional compilation for given CAD
- marked e.g. as `#if ACAD` in the code
- build for platform target `x64`
- target framework according to CAD version: `.NET 8.0` (AutoCAD 2025 - 2026), `.NET Framework 4.8` (AutoCAD 2021 - 2024)
- target framework example: `net8.0-windows` or `net48-windows`

| Compilation symbols | References | 
| --- | --- |
| ACAD .NET 8.0 | `../references/autocad_8.0/` |
| ACAD | `../references/autocad/` |
| BCAD | `../references/bricscad/` |
| GCAD | `../references/gstarcad/` |
| ZCAD | `../references/zwcad/` |


# Architecture

We are building Windows Presentation Foundation (WPF) desktop application. Its architecture is based on [Model-View-ViewModel (MVVM)](https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/february/patterns-wpf-apps-with-the-model-view-viewmodel-design-pattern) design pattern for improved readability and maintainability. Basic structure of the `RailCAD` project consists of the following components:

- `CadInterface`: CAD/Lisp API, communication between RailCAD and host CAD applications
- `Common`: various tools and utils
- `MainApp`: main RailCAD application
- `Models`: data model and backend logic
- `Services`: external services such as `delaunator-sharp`
- `ViewModel`: binding between `Model` and `View`
- `Views`: dialogues and windows

`RailCAD.Tests` contains unit tests for different functionality within the main project.
