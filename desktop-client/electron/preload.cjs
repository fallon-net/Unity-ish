const { contextBridge } = require("electron");

contextBridge.exposeInMainWorld("unityIshDesktop", {
    platform: process.platform,
    version: "0.1.0"
});
