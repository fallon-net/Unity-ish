import { useMemo } from "react";

export function App() {
    const now = useMemo(() => new Date().toLocaleString(), []);

    return (
        <main className="page">
            <h1>Unity-ish Admin</h1>
            <p>Reliability-first local intercom control surface.</p>
            <section className="card">
                <h2>Status</h2>
                <ul>
                    <li>Control API: unknown (wire endpoint check next)</li>
                    <li>LiveKit: unknown (wire endpoint check next)</li>
                    <li>Timestamp: {now}</li>
                </ul>
            </section>
        </main>
    );
}
