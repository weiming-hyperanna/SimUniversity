﻿Feature: Test the set up phase of the game

Scenario: 010 Set up the Catan beginnger's game
	Given the dice roll is predefined to 5 
	When I set up the Catan beginner's game
	Then the university information should be the following:
		| University | Score | Campuses               | Links                   | Students   |
		| Red        | 2     | (0, 3, bl); (0, 2, r)  | (0, 3, bl); (0, 2, tr)  | w, b, s, g |
		| Blue       | 2     | (0, 0, tl); (1, 0, r)  | (0, 1, bl); (1, 0, br)  | 2o, b, s   |
		| White      | 2     | (-1, 3, l); (2, 0, tl) | (-1, 3, tl); (2, 0, tl) | o, w, s    |
		| Orange     | 2     | (2, 2, l); (1, 3, tl)  | (2, 2, bl); (1, 3, tl)  | g, s, w    |
	And the current game phase should be 'Play'
	And the current university of the turn should be 'Red'

Scenario: 020 build a internet link
	When the university build an internet link at (0, 4, tl)
	Then a red internet link should be at (0, 4, tl)
	And the university information should be the following:
		| University | Score | Links                              | Students |
		| Red        | 2     | (0, 3, bl); (0, 2, tr); (0, 4, tl) | s, g     |
		| Blue       | 2     | (0, 1, bl); (1, 0, br)             | 2o, b, s |
		| White      | 2     | (-1, 3, tl); (2, 0, tl)            | o, w, s  |
		| Orange     | 2     | (2, 2, bl); (1, 3, tl)             | g, s, w  |
	And the current game phase should be 'Play'
	And the current university of the turn should be 'Red'

Scenario: 030 End turn
	Given the dice roll is predefined to 4 
	When the turn is ended
	Then the university information should be the following:
		| University | Students   |
		| Red        | w, s, g    |
		| Blue       | 2o, b, s   |
		| White      | o, w, s, b |
		| Orange     | g, s, 2w   |
	And the current university of the turn should be 'Blue'

@ignore
Scenario: 040 End turn
	Given the dice roll is predefined to 5 
	When the turn is ended
	Then the university information should be the following:
		| University | Students |
		| Red        | b, w, o  |
		| Blue       | o, b     |
		| White      | b, o, w  |
		| Orange     | w        |
	And the current university of the turn should be 'White'
