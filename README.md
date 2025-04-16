# 📝 Wordle

A version of the game Wordle™ involving a pair of gRPC services and console client with a focus on distributed systems.

## ✨ Technologies

- `gRPC`
- `Protobuf`
- `C#`
- `.NET 7.0`
- `Visual Studio 2022`

## 🚀 Features

- Incorporates a `Bi-Directional stream` for the Play RPC
- Utilizes a `Mutex` to avoid potential `deadlocks` for multiple users saving to stats file. 
- Multi-Assembly application.
- GameServer acts as both a `client` to WordServer, and `server` to GameClient
- Saves daily user statistics to `json` file and resets on new day (see demo video).

## 📍The Process

This was a group project that I worked on during my 5th semester. The goal of this project was to familiarize ourselves with gRPC protocol and learn how to communicate between different enpoints of a distribute system.

## 🚦Running the Project

1. `Clone` the repository
2. Set solution to use `multiple startup projects` and select all
3. Utilize the client terminal to `play`


## 🎞️ Preview

https://github.com/user-attachments/assets/d2c775b4-2f66-4789-8ac1-f0845f20a7c6


