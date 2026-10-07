This repository should house a web application tailored to my ADHD and specific needs/issues as a tool to aid my day to day work. It should be created and deployed, easy to redeploy through a Github Action, and accessible from a public network. The contents should be private unless unencrypted/de-protected, but this should not require a specific IP address or network to access.

This is targeted towards my day to day work, not personal life. Tools such as Github (issue tracking), Azure Devops (code housing/PRs), Microsoft Loop (Some documentation, limited scope) are included in this. CLIs should be used where possible to automate actions, no headless browsers are allowed for interactions.

## ADHD symptoms to support

- When starting a large, abstract task I can struggle to get started, but then become too engrossed in that specific task until completion - ignoring everything else.
- When an iterative approach to a work item is required, which takes place over a number of days, the state changing from `waiting for other person` to `waiting for me` I will not pick it up, nor see that I need to pick it up.
- I will get distracted by small items which are unimportant elements of a larger task. A good example is a meta-data warning in ServiceTree when I was trying to find SLO definitions. Rather than continuing to find the SLO definitions, I pivoted into digging into why this warning was present. 

## Key Features

- List items of outstanding work, either pending my input or needing starting. This should reflect state changes which put the blocker back onto me, such as a PR reviewer leaving a comment which requires me to make changes.
  - 3 categories, reflected by colour, exist: Critical (red), Warning (yellow), Acceptable (Green)
  - Critical items always exist above Warning items, and Warning items always exist above Acceptable items.
- Order items by priority: 
  - derived from `requires action` date descending, this should capture state changes which have blocked progress by outstanding tasks for me to complete.
  - secondarily date/time for completion compared to today's date, where the date of completion is looming and this work needs to be picked up and completed ASAP.
- Input area to add onto the list, with easy-access buttons to quickly track something that needs doing:
  - Date of completion should allow for raw date input, but also offer 1/3/7 day quick-select buttons
  - Title/body input fields
  - Priority, as a number.
- Item's fields can be manually overridden, for example a stale `Critical` state can be manually changed to `Warning` or `Acceptable` but this is not only limited to this category.
- Ideally a nudge mechanism, something which will prompt me that a work item is not progressing and is pending in a `Critical` state too. This may look like a remote and a locally running service communicating, but there may be a better way.
  - One of my core issues is when a task is waiting for input from someone else for more than 10 minutes, I will not return to that task when it is unblocked for me. I need something to remind me that the state has changed, I need to address something and it's important than I do so.

## Accessibility

Colours should be muted, but distinct. View port should support both mobile (iPhone 16) or Desktop.

## Remote access

Being able to add an item to my list remotely is a key part of this, and should be quick/easy from any device. Being able to load the page, quickly type something in and have it reflect onto the list is key.

## Technical stack

Ideally the entire application, database included, can be deployed as a single artefact. A container makes the most sense, if possible deploy it onto my MCAPS subscription. There may be public networking policies which restrict this, so if this isn't possible do not attempt deployment and tell me why.

The backend should be a .NET 10 application.

The front end should be React (latest).