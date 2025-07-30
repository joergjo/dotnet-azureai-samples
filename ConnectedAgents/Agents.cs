namespace ConnectedAgents;

public static class Agents
{
    public static readonly string PriorityAgentName = "priority_agent";

    public static readonly string PriorityAgentInstructions = """
                                                              Assess how urgent a ticket is based on its description.

                                                              Respond with one of the following levels:
                                                              - High: User-facing or blocking issues
                                                              - Medium: Time-sensitive but not breaking anything
                                                              - Low: Cosmetic or non-urgent tasks

                                                              Only output the urgency level and a very brief explanation.
                                                              """;

    public static readonly string TeamAgentName = "team_agent";

    public static readonly string TeamAgentInstructions = """
                                                          Decide which team should own each ticket.

                                                          Choose from the following teams:
                                                          - Frontend
                                                          - Backend
                                                          - Infrastructure
                                                          - Marketing

                                                          Base your answer on the content of the ticket. Respond with the team name and a very brief explanation.
                                                          """;

    public static readonly string EffortAgentName = "effort_agent";

    public static readonly string EffortAgentInstructions = """
                                                            Estimate how much work each ticket will require.

                                                            Use the following scale:
                                                            - Small: Can be completed in a day
                                                            - Medium: 2-3 days of work
                                                            - Large: Multi-day or cross-team effort

                                                            Base your estimate on the complexity implied by the ticket. Respond with the effort level and a brief justification.
                                                            """;

    public static readonly string TriageAgentInstructions = """
                                                            Triage the given ticket. Use the connected tools to determine the ticket's priority, 
                                                            which team it should be assigned to, and how much effort it may take.
                                                            """;
}