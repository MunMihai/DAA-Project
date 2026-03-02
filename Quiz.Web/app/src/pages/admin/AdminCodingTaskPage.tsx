import { useState } from "react";
import { useApi } from "../../api/axios";
import type { CodingRuleset } from "../../api/codingApi";
import { codingApi } from "../../api/codingApi";

export function AdminCodingTaskPage() {
    const api = useApi();
    const coding = codingApi(api);

    const [referenceCode, setReferenceCode] = useState("");
    const [ruleset, setRuleset] = useState<CodingRuleset | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const handleGenerate = async () => {
        if (!referenceCode.trim()) return;
        setLoading(true);
        setError("");
        try {
            const result = await coding.generateRuleset(referenceCode);
            setRuleset(result);
        } catch (err: any) {
            setError(err.response?.data || err.message || "Failed to generate ruleset.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="max-w-4xl mx-auto p-4">
            <h1 className="text-2xl font-bold mb-4">Coding Quiz Editor (Professor)</h1>

            <p className="mb-2 text-gray-700">1. Paste your reference code:</p>
            <textarea
                className="w-full h-64 p-3 border rounded-lg font-mono text-sm bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 mb-4"
                value={referenceCode}
                onChange={(e: any) => setReferenceCode(e.target.value)}
                placeholder="public class Solution { ... }"
            />

            <button
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-400"
                onClick={handleGenerate}
                disabled={loading || !referenceCode.trim()}
            >
                {loading ? "Generating..." : "Generate Ruleset"}
            </button>

            {error && (
                <p className="text-red-600 mt-4">{error}</p>
            )}

            {ruleset && (
                <div className="p-4 mt-6 bg-gray-100 rounded-lg">
                    <h2 className="text-lg font-semibold mb-2">2. Generated Ruleset (JSON):</h2>
                    <pre className="whitespace-pre-wrap font-mono text-sm bg-white p-3 rounded border">
                        {JSON.stringify(ruleset, null, 2)}
                    </pre>
                </div>
            )}
        </div>
    );
}
