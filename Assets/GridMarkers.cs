using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GridMarker : MonoBehaviour
{
    // Grid bounds (0-6 for X, 0-5 for Y)
    private const int MAX_X = 6;
    private const int MAX_Y = 5;
    
    // Current grid position (integers)
    private int gridX = 0;
    private int gridY = 0;
    
    // Grid settings
    private const float CELL_SIZE = 2f;
    private const float ORIGIN_X = 0f;
    private const float ORIGIN_Y = 0f;

    // Path display settings
    [Header("Path Display")]
    public GameObject pathSegmentPrefab;
    public int pathLength = 7;
    public int numberOfTurns = 1;
    public float displayTime = 3f;
    public float segmentDelay = 0.3f;

    // Path colors
    public Color tailColor = Color.red;
    public Color bodyColor = Color.blue;
    public Color headColor = Color.green;

    // UI Elements
    [Header("UI")]
    public Text messageText;
    public Text scoreText;

    // Score tracking
    private int pathsCount = 0;
    private int successCount = 0;
    private int failCount = 0;

    // Chances system
    [Header("Gameplay")]
    public int chancesPerPath = 2;
    private int remainingChances = 0;

    // Game states
    private enum GameState
    {
        Idle,           // Initial state, press Space to start
        ShowingPath,    // Displaying target path
        WaitForTail,    // User moves to find tail, press Space to start drawing
        Drawing,        // User is drawing their path
        Success,        // User completed path correctly
        Fail            // User made a mistake
    }

    private GameState currentState = GameState.Idle;

    // Target path (generated)
    private List<Vector2Int> targetPath = new List<Vector2Int>();
    private List<GameObject> targetPathSegments = new List<GameObject>();

    // User's drawn path
    private List<Vector2Int> userPath = new List<Vector2Int>();
    private List<GameObject> userPathSegments = new List<GameObject>();

    // Track visited cells during drawing
    private HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();

    // Reference to player's sprite renderer
    private SpriteRenderer playerSpriteRenderer;

    void Start()
    {
        gridX = 0;
        gridY = 0;
        UpdateWorldPosition();
        currentState = GameState.Idle;
        ShowMessage("Press SPACE to start");
        UpdateScoreDisplay();
        
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        switch (currentState)
        {
            case GameState.Idle:
                HandleIdleState();
                break;
            case GameState.WaitForTail:
                HandleWaitForTailState();
                break;
            case GameState.Drawing:
                HandleDrawingState();
                break;
            case GameState.Success:
            case GameState.Fail:
                break;
        }
    }

    void HandleIdleState()
    {
        HandleMovement();
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateNewPath();
        }
    }

    void HandleWaitForTailState()
    {
        HandleMovement();
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartDrawing();
        }
    }

    void HandleDrawingState()
    {
        // Space to finish and check the path (at any time)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CheckResult();
            return;
        }

        // Handle movement and drawing
        Vector2Int oldPos = new Vector2Int(gridX, gridY);
        bool moved = HandleMovementAndReturnMoved();
        
        if (moved)
        {
            Vector2Int newPos = new Vector2Int(gridX, gridY);
            
            // Check if revisiting a cell
            if (visitedCells.Contains(newPos))
            {
                remainingChances--;
                Debug.Log("Crossed path! Remaining chances: " + remainingChances);
                
                if (remainingChances > 0)
                {
                    ShowMessage("Oops! " + remainingChances + " chance(s) left");
                    ClearUserPath();
                    currentState = GameState.Fail;
                    StartCoroutine(RetryWithSamePath());
                }
                else
                {
                    pathsCount++;
                    failCount++;
                    UpdateScoreDisplay();
                    currentState = GameState.Fail;
                    StartCoroutine(HandleFinalFailure());
                }
                return;
            }

            // Add new position to user path
            AddUserPathSegment(newPos);
            
            // Check if path is complete (reached target length)
            if (userPath.Count == targetPath.Count)
            {
                // Change last segment to head color
                if (userPathSegments.Count > 0)
                {
                    SpriteRenderer sr = userPathSegments[userPathSegments.Count - 1].GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = headColor;
                }
                ShowMessage("Done! Press SPACE to check");
            }
        }
    }

    void HandleMovement()
    {
        HandleMovementAndReturnMoved();
    }

    bool HandleMovementAndReturnMoved()
    {
        bool moved = false;

        if (Input.GetKeyDown(KeyCode.RightArrow) && gridX < MAX_X)
        {
            gridX++;
            moved = true;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) && gridX > 0)
        {
            gridX--;
            moved = true;
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) && gridY < MAX_Y)
        {
            gridY++;
            moved = true;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) && gridY > 0)
        {
            gridY--;
            moved = true;
        }

        if (moved)
        {
            UpdateWorldPosition();
        }

        return moved;
    }

    void UpdateWorldPosition()
    {
        float worldX = ORIGIN_X + (gridX * CELL_SIZE);
        float worldY = ORIGIN_Y + (gridY * CELL_SIZE);
        transform.position = new Vector3(worldX, worldY, -0.05f);
    }

    void GenerateNewPath()
    {
        targetPath.Clear();
        
        // Reset chances for new path
        remainingChances = chancesPerPath;
        
        // Try to generate valid path (max 100 attempts)
        bool validPath = false;
        for (int attempt = 0; attempt < 100 && !validPath; attempt++)
        {
            validPath = TryGeneratePath();
        }

        if (validPath)
        {
            StartCoroutine(DisplayTargetPathCoroutine(true));
        }
        else
        {
            Debug.LogWarning("Could not generate valid path");
        }
    }

    bool TryGeneratePath()
    {
        targetPath.Clear();
        HashSet<Vector2Int> usedCells = new HashSet<Vector2Int>();

        // Random start position
        int startX = Random.Range(0, MAX_X + 1);
        int startY = Random.Range(0, MAX_Y + 1);
        Vector2Int startPos = new Vector2Int(startX, startY);
        targetPath.Add(startPos);
        usedCells.Add(startPos);

        // Directions: 0=right, 1=up, 2=left, 3=down
        int[] dx = { 1, 0, -1, 0 };
        int[] dy = { 0, 1, 0, -1 };

        // Pick random initial direction
        int currentDir = Random.Range(0, 4);

        // Calculate segment lengths between turns
        List<int> segmentLengths = DivideLength(pathLength - 1, numberOfTurns + 1);

        int currentX = startX;
        int currentY = startY;

        for (int seg = 0; seg < segmentLengths.Count; seg++)
        {
            int segLength = segmentLengths[seg];

            // Move in current direction for segLength cells
            for (int i = 0; i < segLength; i++)
            {
                currentX += dx[currentDir];
                currentY += dy[currentDir];

                // Check bounds
                if (currentX < 0 || currentX > MAX_X || currentY < 0 || currentY > MAX_Y)
                {
                    return false; // Invalid path - out of bounds
                }

                Vector2Int newPos = new Vector2Int(currentX, currentY);

                // Check if cell already used (path crosses itself)
                if (usedCells.Contains(newPos))
                {
                    return false; // Invalid path - crosses itself
                }

                targetPath.Add(newPos);
                usedCells.Add(newPos);
            }

            // Turn 90 degrees (if not last segment)
            if (seg < segmentLengths.Count - 1)
            {
                // Turn left or right randomly
                if (Random.Range(0, 2) == 0)
                    currentDir = (currentDir + 1) % 4; // Turn left
                else
                    currentDir = (currentDir + 3) % 4; // Turn right
            }
        }

        return true;
    }

    List<int> DivideLength(int totalLength, int parts)
    {
        List<int> lengths = new List<int>();
        
        if (parts <= 0 || totalLength <= 0)
        {
            lengths.Add(totalLength);
            return lengths;
        }

        int remaining = totalLength;
        for (int i = 0; i < parts - 1; i++)
        {
            // Each part gets at least 1, leave enough for remaining parts
            int maxForThis = remaining - (parts - 1 - i);
            int minForThis = 1;
            int len = Random.Range(minForThis, maxForThis + 1);
            lengths.Add(len);
            remaining -= len;
        }
        lengths.Add(remaining); // Last part gets the rest

        return lengths;
    }

    IEnumerator DisplayTargetPathCoroutine(bool isNewPath)
    {
        currentState = GameState.ShowingPath;
        ClearTargetPathSegments();
        SetPlayerVisible(false);

        ShowMessage("Watch the path!");

        // Spawn segments one by one with delay
        for (int i = 0; i < targetPath.Count; i++)
        {
            Vector2Int gridPos = targetPath[i];
            float worldX = ORIGIN_X + (gridPos.x * CELL_SIZE);
            float worldY = ORIGIN_Y + (gridPos.y * CELL_SIZE);

            GameObject segment = Instantiate(pathSegmentPrefab, 
                new Vector3(worldX, worldY, 0f), Quaternion.identity);

            // Set color based on position
            SpriteRenderer sr = segment.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (i == 0)
                    sr.color = tailColor;      // First = tail (red)
                else if (i == targetPath.Count - 1)
                    sr.color = headColor;      // Last = head (green)
                else
                    sr.color = bodyColor;      // Middle = body (blue)
            }

            targetPathSegments.Add(segment);

            // Wait before showing next segment
            yield return new WaitForSeconds(segmentDelay);
        }

        ShowMessage("Memorize it!");

        // Wait for display time with full path visible
        yield return new WaitForSeconds(displayTime);

        // Remove all segments
        ClearTargetPathSegments();

        // Transition to wait for tail state
        currentState = GameState.WaitForTail;
        SetPlayerVisible(true);
        ShowMessage("Your turn!");
    }

    void ClearTargetPathSegments()
    {
        foreach (GameObject seg in targetPathSegments)
        {
            if (seg != null) Destroy(seg);
        }
        targetPathSegments.Clear();
    }

    void StartDrawing()
    {
        currentState = GameState.Drawing;
        userPath.Clear();
        visitedCells.Clear();
        ClearUserPath();

        // Add starting position as tail
        Vector2Int startPos = new Vector2Int(gridX, gridY);
        AddUserPathSegment(startPos, true); // true = is tail

        ShowMessage("Drawing... trace the path!");
    }

    void AddUserPathSegment(Vector2Int gridPos, bool isTail = false)
    {
        userPath.Add(gridPos);
        visitedCells.Add(gridPos);

        float worldX = ORIGIN_X + (gridPos.x * CELL_SIZE);
        float worldY = ORIGIN_Y + (gridPos.y * CELL_SIZE);

        GameObject segment = Instantiate(pathSegmentPrefab,
            new Vector3(worldX, worldY, -0.1f), Quaternion.identity); // Slightly in front

        SpriteRenderer sr = segment.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = isTail ? tailColor : bodyColor;
        }

        userPathSegments.Add(segment);
    }

    void ClearUserPath()
    {
        foreach (GameObject seg in userPathSegments)
        {
            if (seg != null) Destroy(seg);
        }
        userPathSegments.Clear();
        userPath.Clear();
        visitedCells.Clear();
    }

    void CheckResult()
    {
        bool success = true;

        // Check if paths match
        if (userPath.Count != targetPath.Count)
        {
            success = false;
        }
        else
        {
            for (int i = 0; i < userPath.Count; i++)
            {
                if (!userPath[i].Equals(targetPath[i]))
                {
                    success = false;
                    break;
                }
            }
        }

        if (success)
        {
            pathsCount++;
            successCount++;
            UpdateScoreDisplay();
            ShowMessage("Great job!");
            currentState = GameState.Success;
            StartCoroutine(HandleSuccess());
        }
        else
        {
            remainingChances--;
            Debug.Log("Wrong path! Remaining chances: " + remainingChances);
            
            if (remainingChances > 0)
            {
                ShowMessage("Not quite! " + remainingChances + " chance(s) left");
                ClearUserPath();
                currentState = GameState.Fail;
                StartCoroutine(RetryWithSamePath());
            }
            else
            {
                pathsCount++;
                failCount++;
                UpdateScoreDisplay();
                currentState = GameState.Fail;
                StartCoroutine(HandleFinalFailure());
            }
        }
    }

    IEnumerator RetryWithSamePath()
    {
        yield return new WaitForSeconds(1.5f);
        StartCoroutine(DisplayTargetPathCoroutine(false));
    }

    IEnumerator HandleSuccess()
    {
        yield return new WaitForSeconds(1.5f);
        ClearUserPath();
        GenerateNewPath();
    }

    IEnumerator HandleFinalFailure()
    {
        // Show message
        ShowMessage("Nice try! Here's the correct path...");
        ClearUserPath();
        SetPlayerVisible(false);
        
        yield return new WaitForSeconds(1f);
        
        // Show the correct path
        for (int i = 0; i < targetPath.Count; i++)
        {
            Vector2Int gridPos = targetPath[i];
            float worldX = ORIGIN_X + (gridPos.x * CELL_SIZE);
            float worldY = ORIGIN_Y + (gridPos.y * CELL_SIZE);

            GameObject segment = Instantiate(pathSegmentPrefab, 
                new Vector3(worldX, worldY, 0f), Quaternion.identity);

            SpriteRenderer sr = segment.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (i == 0)
                    sr.color = tailColor;
                else if (i == targetPath.Count - 1)
                    sr.color = headColor;
                else
                    sr.color = bodyColor;
            }

            targetPathSegments.Add(segment);
            yield return new WaitForSeconds(segmentDelay);
        }
        
        // Wait for user to see the correct path
        yield return new WaitForSeconds(displayTime);
        
        // Clear and show new path message
        ClearTargetPathSegments();
        ShowMessage("Let's try a new one!");
        
        yield return new WaitForSeconds(1.5f);
        
        // Generate new path
        GenerateNewPath();
    }

    void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
        Debug.Log(message);
    }

    void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = "Paths: " + pathsCount + "\nSuccess: " + successCount + "\nFail: " + failCount;
        }
    }

    void SetPlayerVisible(bool visible)
    {
        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.enabled = visible;
        }
    }
}
