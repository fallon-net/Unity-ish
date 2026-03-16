const { app, BrowserWindow } = require("electron");
const path = require("path");

const isDev = !app.isPackaged;

function createWindow() {
    const window = new BrowserWindow({
        width: 1440,
        height: 940,
        minWidth: 1100,
        minHeight: 760,
        backgroundColor: "#e8edf3",
        webPreferences: {
            preload: path.join(__dirname, "preload.cjs"),
            contextIsolation: true,
            nodeIntegration: false
        }
    });

    if (isDev) {
        window.loadURL("http://localhost:5174");
    } else {
        window.loadFile(path.join(__dirname, "..", "dist", "index.html"));
    }
}

app.whenReady().then(() => {
    createWindow();

    app.on("activate", () => {
        if (BrowserWindow.getAllWindows().length === 0) {
            createWindow();
        }
    });
});

app.on("window-all-closed", () => {
    if (process.platform !== "darwin") {
        app.quit();
    }
});
