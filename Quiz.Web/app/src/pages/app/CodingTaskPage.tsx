import { useState } from "react";
import { useApi } from "../../api/axios";
import type { CodingRuleset, ValidationResult } from "../../api/codingApi";
import { codingApi } from "../../api/codingApi";

export function CodingTaskPage() {
    const api = useApi();
    const coding = codingApi(api);

    // In mod normal, `ruleset`-ul ar veni de pe server din baza de date
    // Pentru MVP, lasam studentul sa dea paste la Ruleset-ul JSON.
    const [rulesetJson, setRulesetJson] = useState("");
    const [studentCode, setStudentCode] = useState("");
    const [result, setResult] = useState<ValidationResult | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const handleEvaluate = async () => {
        if (!studentCode.trim() || !rulesetJson.trim()) return;
        setLoading(true);
        setError("");
        setResult(null);

        try {
            const ruleset: CodingRuleset = JSON.parse(rulesetJson);
            const res = await coding.evaluate(studentCode, ruleset);
            setResult(res);
        } catch (err: any) {
            setError(err.response?.data || err.message || "Failed to evaluate code.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="max-w-4xl mx-auto p-4">
            <h1 className="text-2xl font-bold mb-4">Solve Coding Task</h1>

            <p className="mb-2 text-gray-700">Ruleset (JSON) - For MVP manual paste:</p>
            <textarea
                className="w-full h-32 p-3 border rounded-lg font-mono text-sm bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 mb-6"
                value={rulesetJson}
                onChange={(e: any) => setRulesetJson(e.target.value)}
                placeholder='{"name": "...", "rules": [...] }'
            />

            <p className="mb-2 text-gray-700">Your Solution (C#):</p>
            <textarea
                className="w-full h-64 p-3 border rounded-lg font-mono text-sm bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 mb-4"
                value={studentCode}
                onChange={(e: any) => setStudentCode(e.target.value)}
                placeholder="public class Solution { ... }"
            />

            <button
                className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:bg-gray-400"
                onClick={handleEvaluate}
                disabled={loading || !studentCode.trim() || !rulesetJson.trim()}
            >
                {loading ? "Evaluating..." : "Submit Code"}
            </button>

            {error && (
                <div className="mt-4 p-3 bg-red-100 text-red-700 rounded-lg">
                    {error}
                </div>
            )}

            {result && (
                <div className={`p-4 mt-6 rounded-lg ${result.passed ? 'bg-green-100' : 'bg-red-100'}`}>
                    <h2 className={`text-lg font-bold mb-2 ${result.passed ? 'text-green-700' : 'text-red-700'}`}>
                        {result.passed ? "Passed! Your code meets all requirements." : "Verification Failed"}
                    </h2>

                    {!result.passed && result.violations.length > 0 && (
                        <div className="mt-2">
                            <h3 className="font-semibold text-red-800">Violations:</h3>
                            <ul className="list-disc pl-5 mt-1 text-red-800 text-sm">
                                {result.violations.map((v, idx) => (
                                    <li key={idx}><strong>[{v.ruleId}]</strong> {v.message}</li>
                                ))}
                            </ul>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
